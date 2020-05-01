﻿using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.CensusStream;
using squittal.ScrimPlanetmans.Models.ScrimEngine;
using squittal.ScrimPlanetmans.ScrimMatch.Messages;
using squittal.ScrimPlanetmans.ScrimMatch.Models;
using squittal.ScrimPlanetmans.Services.ScrimMatch;
using System;
using System.Collections.Generic;

namespace squittal.ScrimPlanetmans.ScrimMatch
{
    public class ScrimMatchEngine : IScrimMatchEngine
    {
        private readonly IScrimTeamsManager _teamsManager;
        private readonly IWebsocketMonitor _wsMonitor;
        private readonly IScrimMessageBroadcastService _messageService;
        private readonly ILogger<ScrimMatchEngine> _logger;

        private readonly IStatefulTimer _timer;

        public MatchConfiguration MatchConfiguration { get; set; } = new MatchConfiguration();

        private bool _isRunning = false;

        private int _currentRound = 0;

        private int _roundSecondsMax = 900;
        private int _roundSecondsRemaining;
        private MatchTimerTickMessage _latestTimerTickMessage;

        //private int _roundSecondsElapsed = 0;

        private MatchState _matchState = MatchState.Uninitialized;

        private DateTime _matchStartTime;


        public ScrimMatchEngine(IScrimTeamsManager teamsManager, IWebsocketMonitor wsMonitor, IStatefulTimer timer, IScrimMessageBroadcastService messageService, ILogger<ScrimMatchEngine> logger)
        {
            _teamsManager = teamsManager;
            _wsMonitor = wsMonitor;
            _timer = timer;
            _messageService = messageService;
            _logger = logger;

            _messageService.RaiseMatchTimerTickEvent += OnMatchTimerTick;

            _messageService.RaiseTeamOutfitChangeEvent += OnTeamOutfitChangeEvent;
            _messageService.RaiseTeamPlayerChangeEvent += OnTeamPlayerChangeEvent;

            _messageService.RaiseScrimFacilityControlActionEvent += OnFacilityControlEvent;

            //MatchConfiguration = new MatchConfiguration();
            ClearMatch();
        }

        
        public void Start()
        {
            if (_isRunning)
            {
                return;
            }
            
            if (_currentRound == 0)
            {
                InitializeNewMatch();
            }

            InitializeNewRound();

            StartRound();
        }


        public void ClearMatch()
        {
            if (_isRunning)
            {
                EndRound();
            }
            
            _wsMonitor.DisableScoring();
            _wsMonitor.RemoveAllCharacterSubscriptions();
            _messageService.DisableLogging();

            MatchConfiguration = new MatchConfiguration();

            //_wsMonitor.SetFacilitySubscription(MatchConfiguration.FacilityId);
            //_wsMonitor.SetWorldSubscription(MatchConfiguration.WorldId);

            _roundSecondsMax = MatchConfiguration.RoundSecondsTotal;

            _matchState = MatchState.Uninitialized;
            _currentRound = 0;

            _latestTimerTickMessage = null;

            _teamsManager.ClearAllTeams();

            SendMatchStateUpdateMessage();
            SendMatchConfigurationUpdateMessage();
        }

        public void ConfigureMatch(MatchConfiguration configuration)
        {
            MatchConfiguration = configuration;

            _roundSecondsMax = MatchConfiguration.RoundSecondsTotal;

            _wsMonitor.SetFacilitySubscription(MatchConfiguration.FacilityId);
            _wsMonitor.SetWorldSubscription(MatchConfiguration.WorldId);

            SendMatchConfigurationUpdateMessage(); // TODO: why was this commented out before?
        }

        public void EndRound()
        {
            _isRunning = false;
            _matchState = MatchState.Stopped;

            _wsMonitor.DisableScoring();

            // Stop the timer if forcing the round to end (as opposed to timer reaching 0)
            if (GetLatestTimerTickMessage().MatchTimerStatus.State != MatchTimerState.Stopped)
            {
                _timer.Halt();
            }

            _teamsManager.SaveRoundEndScores(_currentRound);

            _messageService.BroadcastSimpleMessage($"Round {_currentRound} ended; scoring diabled");

            SendMatchStateUpdateMessage();

            _messageService.DisableLogging();
        }

        public void InitializeNewMatch()
        {
            _matchStartTime = DateTime.Now;

            if (MatchConfiguration.SaveLogFiles == true)
            {
                _messageService.SetLogFileName(GetLogFileNameWithExtension());
            }
        }

        private string GetLogFileNameWithExtension()
        {
            var fileName = _matchStartTime.ToString("yyyyMMddTHHmmss");

            for (var i = 1; i <= 3; i++)
            {
                var alias = _teamsManager.GetTeamAliasDisplay(i);

                if (string.IsNullOrWhiteSpace(alias))
                {
                    continue;
                }

                fileName = $"{fileName}_{alias}";
            }

            return ($"{fileName}.txt");
        }

        public void InitializeNewRound()
        {
            _currentRound += 1;
            
            _roundSecondsRemaining = _roundSecondsMax;

            _timer.Configure(TimeSpan.FromSeconds(_roundSecondsMax));
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
            _wsMonitor.EnableScoring();

            SendMatchStateUpdateMessage();
        }

        public void PauseRound()
        {
            _isRunning = false;
            _matchState = MatchState.Paused;

            _wsMonitor.DisableScoring();
            _timer.Pause();

            SendMatchStateUpdateMessage();

            _messageService.DisableLogging();
        }

        public void ResetRound()
        {
            _timer.Reset();
            _wsMonitor.DisableScoring();

            _teamsManager.RollBackAllTeamStats(_currentRound);

            _currentRound -= 1;

            if (_currentRound == 0)
            {
                _matchState = MatchState.Uninitialized;
                _latestTimerTickMessage = null;
            }

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
            _wsMonitor.EnableScoring();

            SendMatchStateUpdateMessage();
        }

        public void SubmitPlayersList()
        {
            _wsMonitor.AddCharacterSubscriptions(_teamsManager.GetAllPlayerIds());
        }

        private void OnMatchTimerTick(object sender, MatchTimerTickEventArgs e)
        {
            var message = e.Message;

            SetLatestTimerTickMessage(e.Message);

            var status = message.MatchTimerStatus;

            var state = status.State;

            if (state == MatchTimerState.Stopped && _isRunning)
            {
                EndRound();
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

        public MatchTimerTickMessage GetLatestTimerTickMessage()
        {
            return _latestTimerTickMessage;
        }

        private void SetLatestTimerTickMessage(MatchTimerTickMessage value)
        {
            _latestTimerTickMessage = value;
        }

        private void OnTeamOutfitChangeEvent(object sender, TeamOutfitChangeEventArgs e)
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

            //var worldId = (int)e.Message.Outfit.WorldId;

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

        private void OnTeamPlayerChangeEvent(object sender, TeamPlayerChangeEventArgs e)
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

        private void OnFacilityControlEvent(object sender, ScrimFacilityControlActionEventEventArgs e)
        {
            if (!MatchConfiguration.EndRoundOnFacilityCapture)
            {
                return;
            }

            var message = e.Message;
            var controlEvent = message.FacilityControl;

            if (controlEvent.FacilityId == MatchConfiguration.FacilityId && controlEvent.WorldId == MatchConfiguration.WorldId)
            {
                EndRound();
            }
        }

        private void SendMatchStateUpdateMessage()
        {
            _messageService.BroadcastMatchStateUpdateMessage(new MatchStateUpdateMessage(_matchState, _currentRound, DateTime.UtcNow, MatchConfiguration.Title));
        }

        private void SendMatchConfigurationUpdateMessage()
        {
            _messageService.BroadcastMatchConfigurationUpdateMessage(new MatchConfigurationUpdateMessage(MatchConfiguration));
        }
    }
}
