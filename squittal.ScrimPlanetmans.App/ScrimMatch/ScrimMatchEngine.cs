using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.CensusStream.Interfaces;
using squittal.ScrimPlanetmans.App.ScrimMatch.Events;
using squittal.ScrimPlanetmans.App.ScrimMatch.Interfaces;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;
using squittal.ScrimPlanetmans.App.Services.ScrimMatch.Interfaces;

namespace squittal.ScrimPlanetmans.App.ScrimMatch;

public class ScrimMatchEngine : IScrimMatchEngine
{
    private readonly IScrimTeamsManager _teamsManager;
    private readonly IScrimMessageBroadcastService _messageService;
    private readonly IScrimMatchDataService _matchDataService;
    private readonly IScrimRulesetManager _rulesetManager;
    private readonly ILogger<ScrimMatchEngine> _logger;
    private readonly IEventFilterService _eventFilter;

    private readonly IStatefulTimer _timer;

    public MatchConfiguration MatchConfiguration { get; set; } = new MatchConfiguration();
    public Ruleset.Models.Ruleset MatchRuleset { get; private set; }

    public int CurrentSeriesMatch { get; private set; } = 0;

    private bool _isRunning = false;

    private int _currentRound = 0;

    private int _roundSecondsMax = 900;
    private int _roundSecondsRemaining;
    private MatchTimerTickMessage _latestTimerTickMessage;

    private MatchState _matchState = MatchState.Uninitialized;

    private DateTime _matchStartTime;


    public ScrimMatchEngine
    (
        IScrimTeamsManager teamsManager,
        IStatefulTimer timer,
        IScrimMatchDataService matchDataService,
        IScrimMessageBroadcastService messageService,
        IScrimRulesetManager rulesetManager,
        ILogger<ScrimMatchEngine> logger,
        IEventFilterService eventFilter
    )
    {
        _teamsManager = teamsManager;
        _timer = timer;
        _messageService = messageService;
        _matchDataService = matchDataService;
        _rulesetManager = rulesetManager;
        _logger = logger;
        _eventFilter = eventFilter;

        _messageService.RaiseMatchTimerTickEvent += async (s, e) => await OnMatchTimerTick(s, e);

        _messageService.RaiseTeamOutfitChangeEvent += OnTeamOutfitChangeEvent;
        _messageService.RaiseTeamPlayerChangeEvent += OnTeamPlayerChangeEvent;

        _messageService.RaiseScrimFacilityControlActionEvent += async (s, e) => await OnFacilityControlEvent(s, e);
    }


    public async Task Start()
    {
        if (_isRunning)
        {
            return;
        }

        if (_currentRound == 0)
        {
            await InitializeNewMatch();
        }

        await InitializeNewRound();

        StartRound();
    }


    public async Task ClearMatch(bool isRematch)
    {
        if (_isRunning)
        {
            await EndRound();
        }

        _eventFilter.IsScoringEnabled = false;
        if (!isRematch)
            _eventFilter.RemoveAllCharacters();

        _messageService.DisableLogging();

        var previousWorldId = MatchConfiguration.WorldIdString;
        var previousIsManualWorldId = MatchConfiguration.IsManualWorldId;

        var previousEndRoundOnFacilityCapture = MatchConfiguration.EndRoundOnFacilityCapture;
        var previousIsManualEndRoundOnFacilityCapture = MatchConfiguration.EndRoundOnFacilityCapture;

        MatchConfiguration = new MatchConfiguration();

        if (isRematch)
        {
            MatchConfiguration.TrySetWorldId(previousWorldId, previousIsManualWorldId);
            MatchConfiguration.TrySetEndRoundOnFacilityCapture(previousEndRoundOnFacilityCapture, previousIsManualEndRoundOnFacilityCapture);
        }
        else
        {
            var activeRuleset = await _rulesetManager.GetActiveRulesetAsync();
            MatchConfiguration.TrySetEndRoundOnFacilityCapture(activeRuleset.DefaultEndRoundOnFacilityCapture, false);
        }

        _roundSecondsMax = MatchConfiguration.RoundSecondsTotal;

        _matchState = MatchState.Uninitialized;
        _currentRound = 0;

        _matchDataService.CurrentMatchRound = _currentRound;
        _matchDataService.CurrentMatchId = string.Empty;

        _latestTimerTickMessage = null;

        if (isRematch)
        {
            _teamsManager.UpdateAllTeamsMatchSeriesResults(CurrentSeriesMatch);
            _teamsManager.ResetAllTeamsMatchData();
        }
        else
        {
            CurrentSeriesMatch = 0;

            _teamsManager.ClearAllTeams();
        }

        SendMatchStateUpdateMessage();
        SendMatchConfigurationUpdateMessage();
    }

    public void ConfigureMatch(MatchConfiguration configuration)
    {
        MatchConfiguration = configuration;
        _roundSecondsMax = MatchConfiguration.RoundSecondsTotal;
        SendMatchConfigurationUpdateMessage();
    }

    public bool TrySetMatchRuleset(Ruleset.Models.Ruleset matchRuleset)
    {
        if (_currentRound == 0 && _matchState == MatchState.Uninitialized && !_isRunning)
        {
            MatchRuleset = matchRuleset;
            _matchDataService.CurrentMatchRulesetId = matchRuleset.Id;
            return true;
        }
        else
        {
            return false;
        }
    }

    public async Task EndRound()
    {
        _isRunning = false;
        _matchState = MatchState.Stopped;

        _eventFilter.IsScoringEnabled = false;

        // Stop the timer if forcing the round to end (as opposed to timer reaching 0)
        if (GetLatestTimerTickMessage().MatchTimerStatus.State != MatchTimerState.Stopped)
        {
            _timer.Halt();
        }

        await _teamsManager.SaveRoundEndScores(_currentRound);

        _messageService.BroadcastSimpleMessage($"Round {_currentRound} ended; scoring diabled");

        SendMatchStateUpdateMessage();

        _messageService.DisableLogging();
    }

    public async Task InitializeNewMatch()
    {
        TrySetMatchRuleset(_rulesetManager.ActiveRuleset);

        _matchStartTime = DateTime.UtcNow;

        CurrentSeriesMatch++;

        if (MatchConfiguration.SaveLogFiles == true)
        {
            var matchId = BuildMatchId();

            _messageService.SetLogFileName($"{matchId}.txt");

            var scrimMatch = new Data.Models.ScrimMatch
            {
                Id = matchId,
                StartTime = _matchStartTime,
                Title = MatchConfiguration.Title,
                RulesetId = MatchRuleset.Id
            };

            await _matchDataService.SaveToCurrentMatch(scrimMatch);
        }
    }

    private string BuildMatchId()
    {
        var matchId = _matchStartTime.ToString("yyyyMMddTHHmmss");

        for (var i = 1; i <= 3; i++)
        {
            var alias = _teamsManager.GetTeamAliasDisplay(i);

            if (string.IsNullOrWhiteSpace(alias))
            {
                continue;
            }

            matchId = $"{matchId}_{alias}";
        }

        return matchId;
    }

    public async Task InitializeNewRound()
    {
        _currentRound += 1;

        _matchDataService.CurrentMatchRound = _currentRound;

        _roundSecondsRemaining = _roundSecondsMax;

        _timer.Configure(TimeSpan.FromSeconds(_roundSecondsMax));

        await _matchDataService.SaveCurrentMatchRoundConfiguration(MatchConfiguration);
    }

    public void StartRound()
    {
        _isRunning = true;
        _matchState = MatchState.Running;

        if (MatchConfiguration.SaveLogFiles)
        {
            _messageService.EnableLogging();
        }

        _timer.Start();
        _eventFilter.IsScoringEnabled = true;

        SendMatchStateUpdateMessage();
    }

    public void PauseRound()
    {
        _isRunning = false;
        _matchState = MatchState.Paused;

        _eventFilter.IsScoringEnabled = false;
        _timer.Pause();

        SendMatchStateUpdateMessage();

        _messageService.DisableLogging();
    }

    public async Task ResetRound()
    {
        if (_currentRound == 0)
        {
            return;
        }
            
        _timer.Reset();
        _eventFilter.IsScoringEnabled = false;

        await _teamsManager.RollBackAllTeamStats(_currentRound);

        await _matchDataService.RemoveMatchRoundConfiguration(_currentRound);

        _currentRound -= 1;

        if (_currentRound == 0)
        {
            _matchState = MatchState.Uninitialized;
            _latestTimerTickMessage = null;
        }

        _matchDataService.CurrentMatchRound = _currentRound;

        SendMatchStateUpdateMessage();

        _messageService.DisableLogging();
    }

    public void ResumeRound()
    {
        _isRunning = true;
        _matchState = MatchState.Running;

        if (MatchConfiguration.SaveLogFiles)
        {
            _messageService.EnableLogging();
        }

        _timer.Resume();
        _eventFilter.IsScoringEnabled = true;

        SendMatchStateUpdateMessage();
    }

    public void SubmitPlayersList()
    {
        foreach (string id in _teamsManager.GetAllPlayerIds())
        {
            if (ulong.TryParse(id, out ulong parsed))
                _eventFilter.AddCharacter(parsed);
        }
    }

    private async Task OnMatchTimerTick(object sender, ScrimMessageEventArgs<MatchTimerTickMessage> e)
    {
        var message = e.Message;

        SetLatestTimerTickMessage(e.Message);

        var status = message.MatchTimerStatus;

        var state = status.State;

        if (state == MatchTimerState.Stopped && _isRunning)
        {
            await EndRound();
            return;
        }
    }

    public bool IsRunning()
    {
        return _isRunning;
    }

    public int GetCurrentRound()
    {
        return _currentRound;
    }

    public MatchState GetMatchState()
    {
        return _matchState;
    }

    public string GetMatchId()
    {
        return _matchDataService.CurrentMatchId;
    }

    public MatchTimerTickMessage GetLatestTimerTickMessage()
    {
        return _latestTimerTickMessage;
    }

    private void SetLatestTimerTickMessage(MatchTimerTickMessage value)
    {
        _latestTimerTickMessage = value;
    }

    private void OnTeamOutfitChangeEvent(object sender, ScrimMessageEventArgs<TeamOutfitChangeMessage> e)
    {
        if (MatchConfiguration.IsManualWorldId)
        {
            return;
        }

        int? worldId;

        var message = e.Message;
        var changeType = message.ChangeType;
        bool isRollBack = false;

        if (changeType == TeamChangeType.Add)
        {
            worldId = e.Message.Outfit.WorldId;
        }
        else
        {
            worldId = _teamsManager.GetNextWorldId(MatchConfiguration.WorldId);
            isRollBack = true;
        }

        if (worldId == null)
        {
            MatchConfiguration.ResetWorldId();
            SendMatchConfigurationUpdateMessage();
        }
        else if (MatchConfiguration.TrySetWorldId((int)worldId, false, isRollBack))
        {
            SendMatchConfigurationUpdateMessage();
        }
    }

    private void OnTeamPlayerChangeEvent(object sender, ScrimMessageEventArgs<TeamPlayerChangeMessage> e)
    {
        if (MatchConfiguration.IsManualWorldId)
        {
            return;
        }

        var message = e.Message;
        var changeType = message.ChangeType;
        var player = message.Player;

        // Handle outfit removals via Team Outfit Change events
        if (!player.IsOutfitless)
        {
            return;
        }

        int? worldId;
        bool isRollBack = false;

        if (changeType == TeamPlayerChangeType.Add)
        {
            worldId = player.WorldId;
        }
        else
        {
            worldId = _teamsManager.GetNextWorldId(MatchConfiguration.WorldId);
            isRollBack = true;
        }

        if (worldId == null)
        {
            MatchConfiguration.ResetWorldId();
            SendMatchConfigurationUpdateMessage();
        }
        else if (MatchConfiguration.TrySetWorldId((int)worldId, false, isRollBack))
        {
            SendMatchConfigurationUpdateMessage();
        }
    }

    private async Task OnFacilityControlEvent(object sender, ScrimMessageEventArgs<ScrimFacilityControlActionEventMessage> e)
    {
        if (!MatchConfiguration.EndRoundOnFacilityCapture)
        {
            return;
        }

        var message = e.Message;
        var controlEvent = message.FacilityControl;

        if (controlEvent.FacilityId == MatchConfiguration.FacilityId && controlEvent.WorldId == MatchConfiguration.WorldId)
        {
            await EndRound();
        }
    }

    private void SendMatchStateUpdateMessage()
    {
        _messageService.BroadcastMatchStateUpdateMessage(new MatchStateUpdateMessage(_matchState, _currentRound, DateTime.UtcNow, MatchConfiguration.Title, _matchDataService.CurrentMatchId));
    }

    private void SendMatchConfigurationUpdateMessage()
    {
        _messageService.BroadcastMatchConfigurationUpdateMessage(new MatchConfigurationUpdateMessage(MatchConfiguration));
    }
}
