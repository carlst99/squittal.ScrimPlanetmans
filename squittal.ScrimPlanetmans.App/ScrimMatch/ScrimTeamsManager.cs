using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DbgCensus.Core.Objects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Abstractions.Services.Planetside;
using squittal.ScrimPlanetmans.App.Abstractions.Services.ScrimMatch;
using squittal.ScrimPlanetmans.App.Data;
using squittal.ScrimPlanetmans.App.Data.Models;
using squittal.ScrimPlanetmans.App.Models;
using squittal.ScrimPlanetmans.App.Models.Planetside;
using squittal.ScrimPlanetmans.App.ScrimMatch.Events;
using squittal.ScrimPlanetmans.App.ScrimMatch.Interfaces;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;
using squittal.ScrimPlanetmans.App.Util;

namespace squittal.ScrimPlanetmans.App.ScrimMatch;

public class ScrimTeamsManager : IScrimTeamsManager
{
    private const string DEFAULT_ALIAS_PRE_TEXT = "tm";

    private readonly ILogger<ScrimTeamsManager> _logger;
    private readonly IScrimPlayersService _scrimPlayers;
    private readonly IOutfitService _outfitService;
    private readonly IConstructedTeamService _constructedTeamService;
    private readonly IScrimMessageBroadcastService _messageService;
    private readonly IDbContextFactory<PlanetmansDbContext> _dbContextFactory;
    private readonly IScrimMatchDataService _matchDataService;

    private readonly Dictionary<TeamDefinition, Team> _ordinalTeamMap = new();
    private readonly List<Player> _allPlayers = new();
    private readonly ConcurrentDictionary<ulong, TeamDefinition> _playerTeamMap = new();
    private readonly KeyedSemaphoreSlim _characterMatchDataLock = new();

    public MaxPlayerPointsTracker MaxPlayerPointsTracker { get; private set; } = new();

    public ScrimTeamsManager
    (
        ILogger<ScrimTeamsManager> logger,
        IScrimPlayersService scrimPlayers,
        IOutfitService outfitService,
        IScrimMessageBroadcastService messageService,
        IScrimMatchDataService matchDataService,
        IConstructedTeamService constructedTeamService,
        IDbContextFactory<PlanetmansDbContext> dbContextFactory
    )
    {
        _logger = logger;
        _scrimPlayers = scrimPlayers;
        _outfitService = outfitService;
        _messageService = messageService;
        _matchDataService = matchDataService;
        _constructedTeamService = constructedTeamService;
        _dbContextFactory = dbContextFactory;

        foreach (TeamDefinition teamDef in Enum.GetValues<TeamDefinition>())
            _ordinalTeamMap[teamDef] = new Team(teamDef.ToString(), teamDef.ToString(), teamDef);
    }

    public Team GetTeam(TeamDefinition teamOrdinal)
        => _ordinalTeamMap.TryGetValue(teamOrdinal, out Team? team)
            ? team
            : throw new ArgumentException("Invalid team definition", nameof(teamOrdinal));

    public string GetTeamAliasDisplay(TeamDefinition teamOrdinal)
        => GetTeam(teamOrdinal).Alias;

    public int GetTeamScoreDisplay(TeamDefinition teamOrdinal)
        => GetTeam(teamOrdinal).EventAggregate.Points;

    public Team? GetTeamFromConstructedTeamFaction(int constructedTeamId, FactionDefinition factionId)
        => !IsConstructedTeamFactionAvailable(constructedTeamId, factionId, out Team? owningTeam)
            ? owningTeam
            : null;

    public Team? GetFirstTeamWithFactionId(FactionDefinition factionId)
        => _ordinalTeamMap.Values.FirstOrDefault(team => factionId == team.FactionId);

    public IEnumerable<ulong> GetAllPlayerIds()
    {
        List<ulong> characterIds = new();

        foreach (Team team in _ordinalTeamMap.Values)
            characterIds.AddRange(team.GetAllPlayerIds());

        return characterIds;
    }

    public IEnumerable<Player> GetTeamOutfitPlayers(TeamDefinition teamOrdinal, string outfitAliasLower)
        => GetTeam(teamOrdinal).GetOutfitPlayers(outfitAliasLower);

    public IEnumerable<Player> GetTeamNonOutfitPlayers(TeamDefinition teamOrdinal)
        => GetTeam(teamOrdinal).GetNonOutfitPlayers();

    public IEnumerable<Player> GetTeamConstructedTeamFactionPlayers(TeamDefinition teamOrdinal, int constructedTeamId, FactionDefinition factionId)
        => GetTeam(teamOrdinal).GetConstructedTeamFactionPlayers(constructedTeamId, factionId);

    public WorldDefinition? GetNextWorldId(WorldDefinition previousWorldId)
    {
        if (_allPlayers.Any(p => p.WorldId == previousWorldId))
            return previousWorldId;

        if (_allPlayers.Any())
            return _allPlayers.Where(p => p.WorldId > 0).Select(p => p.WorldId).FirstOrDefault();

        return null;
    }

    private void UpdateTeamFaction(TeamDefinition teamOrdinal, FactionDefinition? factionId)
    {
        Team team = GetTeam(teamOrdinal);
        FactionDefinition? oldFactionId = team.FactionId;

        if (oldFactionId == factionId)
            return;

        team.FactionId = factionId;

        string abbrev = SqCssHelper.GetFactionAbbreviation(factionId);
        string oldAbbrev = SqCssHelper.GetFactionAbbreviation(oldFactionId);

        _messageService.BroadcastTeamFactionChangeMessage(new TeamFactionChangeMessage(teamOrdinal, factionId, abbrev, oldFactionId, oldAbbrev));
        _logger.LogInformation
        (
            "Faction for Team {Ordinal} changed from {Old} to {New}",
            teamOrdinal,
            oldAbbrev,
            abbrev
        );
    }

    public bool UpdateTeamAlias(TeamDefinition teamOrdinal, string alias, bool isCustom = false)
    {
        Team team = GetTeam(teamOrdinal);
        string oldAlias = team.Alias;

        if (!team.TrySetAlias(alias, isCustom))
        {
            _logger.LogInformation
            (
                "Couldn't change {TeamName} display Alias: custom alias already set",
                team.NameInternal
            );
            return false;
        }

        _logger.LogInformation
        (
            "Alias for Team {Ordinal} changed from {Old} to {New}",
            teamOrdinal,
            oldAlias,
            alias
        );

        _messageService.BroadcastTeamAliasChangeMessage(new TeamAliasChangeMessage(teamOrdinal, alias, oldAlias));
        return true;
    }

    public async Task<bool> UpdatePlayerTemporaryAliasAsync(ulong playerId, string newAlias)
    {
        Player? player = GetPlayerFromId(playerId);
        if (player is null)
            return false;

        string oldAlias = player.NameDisplay != player.NameFull
            ? player.NameDisplay
            : string.Empty;

        if (!player.TrySetNameAlias(newAlias))
        {
            _messageService.BroadcastSimpleMessage
            (
                $"<span style=\"color: red; font-weight: 700;\">Couldn't change {player.NameFull} " +
                "match alias: new alias is invalid</span>"
            );
            return false;
        }

        // Send message before updating database so UI/Overlay get updates faster
        _messageService.BroadcastPlayerNameDisplayChangeMessage(new PlayerNameDisplayChangeMessage(player, newAlias, oldAlias));

        if (player.IsParticipating)
            await _matchDataService.SaveMatchParticipatingPlayerAsync(player);

        return true;
    }

    public async Task ClearPlayerDisplayNameAsync(ulong playerId)
    {
        Player? player = GetPlayerFromId(playerId);
        if (player is null)
            return;

        if (string.IsNullOrWhiteSpace(player.NameTrimmed) && string.IsNullOrWhiteSpace(player.NameAlias))
            return;

        string oldAlias = player.NameDisplay != player.NameFull
            ? player.NameDisplay
            : string.Empty;

        player.ClearAllDisplayNameSources();

        // Send message before updating database so UI/Overlay get updates faster
        _messageService.BroadcastPlayerNameDisplayChangeMessage(new PlayerNameDisplayChangeMessage(player, string.Empty, oldAlias));

        if (player.IsParticipating)
            await _matchDataService.SaveMatchParticipatingPlayerAsync(player);
    }

    public async Task<bool> TryAddFreeTextInputCharacterToTeamAsync(TeamDefinition teamOrdinal, string inputString)
    {
        Regex idRegex = new("[0-9]{19}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        bool isId = idRegex.Match(inputString).Success;

        if (isId && await TryAddCharacterIdToTeamAsync(teamOrdinal, ulong.Parse(inputString)))
            return true;

        Regex nameRegex = new("[A-Za-z0-9]{1,32}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        bool isName = nameRegex.Match(inputString).Success;

        return isName && await TryAddCharacterNameToTeam(teamOrdinal, inputString);
    }

    public async Task<bool> TryAddCharacterIdToTeamAsync(TeamDefinition teamOrdinal, ulong characterId)
    {
        if (!IsCharacterAvailable(characterId))
            return false;

        Player? player = await _scrimPlayers.GetByIdAsync(characterId);

        return player is not null && TryAddPlayerToTeam(teamOrdinal, player);
    }

    public async Task<bool> TryAddCharacterNameToTeam(TeamDefinition teamOrdinal, string characterName)
    {
        Player? player = await _scrimPlayers.GetByNameAsync(characterName);
        if (player is null)
            return false;

        return IsCharacterAvailable(player.Id) && TryAddPlayerToTeam(teamOrdinal, player);
    }

    private bool TryAddPlayerToTeam(TeamDefinition teamOrdinal, Player player)
    {
        Team team = GetTeam(teamOrdinal);

        player.TeamOrdinal = team.TeamOrdinal;
        player.IsOutfitless = !IsOutfitOwnedByTeam(player.OutfitAliasLower, out _);

        if (team.TryAddPlayer(player))
        {
            _allPlayers.Add(player);

            _playerTeamMap.TryAdd(player.Id, teamOrdinal);

            if (team.FactionId == null)
            {
                UpdateTeamFaction(teamOrdinal, player.FactionId);
            }

            SendTeamPlayerAddedMessage(player);

            return true;
        }
        else
        {
            return false;
        }
    }

    #region Add Entities To Teams

    public async Task<bool> AddOutfitAliasToTeam(TeamDefinition teamOrdinal, string aliasLower)
    {
        if (!IsOutfitOwnedByTeam(aliasLower, out _))
            return false;

        /* Add Outfit to Team */
        Outfit? outfit = await _outfitService.GetByAliasAsync(aliasLower);
        if (outfit == null)
            return false;

        outfit.TeamOrdinal = teamOrdinal;

        Team team = GetTeam(teamOrdinal);
        if (!team.TryAddOutfit(outfit))
            return false;

        // If not yet set, set team alias to alias of the first outfit added to it
        if (TeamOutfitCount(teamOrdinal) == 1 && TeamConstructedTeamCount(teamOrdinal) == 0)
            UpdateTeamAlias(teamOrdinal, outfit.Alias);

        if (team.FactionId == null && outfit.FactionId != null)
            UpdateTeamFaction(teamOrdinal, outfit.FactionId);

        SendTeamOutfitAddedMessage(outfit);


        /* Add Outfit Players to Team */
        TeamOutfitChangeMessage loadStartedMessage = new(outfit, TeamChangeType.OutfitMembersLoadStarted);
        _messageService.BroadcastTeamOutfitChangeMessage(loadStartedMessage);

        TeamOutfitChangeMessage loadCompleteMessage = new(outfit, TeamChangeType.OutfitMembersLoadCompleted);

        IEnumerable<Player>? getPlayers = await _scrimPlayers.GetPlayersFromOutfitAliasAsync(aliasLower);
        if (getPlayers is null)
        {
            _messageService.BroadcastTeamOutfitChangeMessage(loadCompleteMessage);
            return false;
        }

        Player[] players = getPlayers.ToArray();
        if (players.Length is 0)
        {
            _messageService.BroadcastTeamOutfitChangeMessage(loadCompleteMessage);
            return false;
        }

        bool anyPlayersAdded = false;
        Player lastPlayer = players[^1];

        //TODO: track which players were added and which weren't

        foreach (Player player in players)
        {
            player.TeamOrdinal = teamOrdinal;
            player.FactionId = outfit.FactionId ?? 0;
            player.WorldId = outfit.WorldId ?? 0;

            player.UpdateNameTrimmed();

            if (!team.TryAddPlayer(player))
                continue;

            _allPlayers.Add(player);
            _playerTeamMap.TryAdd(player.Id, teamOrdinal);
            bool isLastPlayer = (player == lastPlayer);
            SendTeamPlayerAddedMessage(player, isLastPlayer);

            anyPlayersAdded = true;
        }

        int newMemberCount = GetTeam(teamOrdinal).Players
            .Count(p => p.OutfitAliasLower == aliasLower && !p.IsOutfitless);
        outfit.MemberCount = newMemberCount;

        int newOnlineCount = GetTeam(teamOrdinal).Players
            .Count(p => p.OutfitAliasLower == aliasLower && !p.IsOutfitless && p.IsOnline);
        outfit.MembersOnlineCount = newOnlineCount;

        _messageService.BroadcastTeamOutfitChangeMessage(loadCompleteMessage);

        return anyPlayersAdded;
    }

    public async Task<bool> AddConstructedTeamFactionMembersToTeam(TeamDefinition teamOrdinal, int constructedTeamId, FactionDefinition factionId)
    {
        if (!IsConstructedTeamFactionAvailable(constructedTeamId, factionId))
        {
            return false;
        }

        Team owningTeam = GetTeam(teamOrdinal);

        ConstructedTeam? constructedTeam = await _constructedTeamService.GetConstructedTeamAsync(constructedTeamId, true);
        if (constructedTeam is null)
            return false;

        ConstructedTeamMatchInfo matchInfo = new()
        {
            ConstructedTeam = constructedTeam,
            TeamOrdinal = teamOrdinal,
            ActiveFactionId = factionId
        };

        if (!owningTeam.TryAddConstructedTeamFaction(matchInfo))
        {
            return false;
        }

        // If not yet set, set team alias to alias of the first constructed team added to it
        if (TeamOutfitCount(teamOrdinal) == 0 && TeamConstructedTeamCount(teamOrdinal) == 1 && owningTeam.Alias == $"{ DEFAULT_ALIAS_PRE_TEXT}{teamOrdinal}")
        {
            UpdateTeamAlias(teamOrdinal, constructedTeam.Alias);
        }

        if (owningTeam.FactionId == null)
        {
            UpdateTeamFaction(teamOrdinal, factionId);
        }

        TeamConstructedTeamChangeMessage message = new(teamOrdinal, constructedTeam, factionId, TeamChangeType.Add);
        _messageService.BroadcastTeamConstructedTeamChangeMessage(message);


        TeamConstructedTeamChangeMessage loadStartedMessage = new(teamOrdinal, constructedTeam, factionId, TeamChangeType.ConstructedTeamMembersLoadStarted);
        _messageService.BroadcastTeamConstructedTeamChangeMessage(loadStartedMessage);

        TeamConstructedTeamChangeMessage loadCompletedMessage = new(teamOrdinal, constructedTeam, factionId, TeamChangeType.ConstructedTeamMembersLoadCompleted);

        IEnumerable<Player>? players = await _constructedTeamService.GetConstructedTeamFactionPlayersAsync(constructedTeamId, factionId);

        if (players == null || !players.Any())
        {
            _messageService.BroadcastTeamConstructedTeamChangeMessage(loadCompletedMessage);
            return false;
        }

        bool anyPlayersAdded = false;
        int playersAddedCount = 0;

        Player? lastPlayer = players.LastOrDefault();

        //TODO: track which players were added and which weren't

        foreach (Player player in players)
        {
            if (!IsCharacterAvailable(player.Id))
            {
                continue;
            }

            player.TeamOrdinal = teamOrdinal;
            player.ConstructedTeamId = constructedTeamId;

            player.IsOutfitless = true;

            if (owningTeam.TryAddPlayer(player))
            {
                _allPlayers.Add(player);

                _playerTeamMap.TryAdd(player.Id, teamOrdinal);

                bool isLastPlayer = (player == lastPlayer);

                SendTeamPlayerAddedMessage(player, isLastPlayer);

                anyPlayersAdded = true;
                playersAddedCount += 1;
            }
        }

        loadCompletedMessage = new TeamConstructedTeamChangeMessage(teamOrdinal, constructedTeam, factionId, TeamChangeType.ConstructedTeamMembersLoadCompleted, playersAddedCount);

        _messageService.BroadcastTeamConstructedTeamChangeMessage(loadCompletedMessage);

        return anyPlayersAdded;
    }
    #endregion Add Entities To Teams

    public async Task<bool> RefreshOutfitPlayers(string aliasLower)
    {
        if (IsOutfitOwnedByTeam(aliasLower, out Team outfitTeam))
        {
            return false;
        }

        TeamDefinition teamOrdinal = outfitTeam.TeamOrdinal;

        IEnumerable<Player>? players = await _scrimPlayers.GetPlayersFromOutfitAliasAsync(aliasLower);

        if (players == null || !players.Any())
        {
            return false;
        }

        List<Player> newPlayers = players.Where(p => IsCharacterAvailable(p.Id)).ToList();

        List<Player> oldPlayers = players.Where(p => !newPlayers.Contains(p)).ToList();

        foreach (Player player in oldPlayers)
        {
            bool oldOnlineStatus = GetPlayerFromId(player.Id).IsOnline;
            bool newOnlineStatus = player.IsOnline;

            if (oldOnlineStatus != newOnlineStatus)
            {
                SetPlayerOnlineStatus(player.Id, newOnlineStatus);
            }
        }

        if (!newPlayers.Any())
        {
            return false;
        }

        Player? lastPlayer = newPlayers.LastOrDefault();

        Outfit? outfit = outfitTeam.Outfits
            .FirstOrDefault(o => o.AliasLower == aliasLower);

        FactionDefinition? outfitFactionID = outfit?.FactionId;
        WorldDefinition outfitWorldId = outfit.WorldId ?? 0;

        bool anyPlayersAdded = false;

        //TODO: track which players were added and which weren't

        foreach (Player player in newPlayers)
        {
            bool isLastPlayer = (player == lastPlayer);

            player.TeamOrdinal = teamOrdinal;
            player.FactionId = outfitFactionID ?? FactionDefinition.None;
            player.WorldId = outfitWorldId;

            player.UpdateNameTrimmed();

            if (outfitTeam.TryAddPlayer(player))
            {
                _allPlayers.Add(player);

                _playerTeamMap.TryAdd(player.Id, teamOrdinal);

                SendTeamPlayerAddedMessage(player, isLastPlayer);

                anyPlayersAdded = true;
            }
        }

        int newMemberCount = GetTeam(teamOrdinal).Players.Count(p => p.OutfitAliasLower == aliasLower && !p.IsOutfitless);
        outfit.MemberCount = newMemberCount;

        int newOnlineCount = GetTeam(teamOrdinal).Players.Count(p => p.OutfitAliasLower == aliasLower && !p.IsOutfitless && p.IsOnline);
        outfit.MembersOnlineCount = newOnlineCount;

        TeamOutfitChangeMessage loadCompleteMessage = new(outfit, TeamChangeType.OutfitMembersLoadCompleted);
        _messageService.BroadcastTeamOutfitChangeMessage(loadCompleteMessage);

        return anyPlayersAdded;
    }

    #region Remove Entities From Teams
    public async Task<bool> RemoveOutfitFromTeamAndDbAsync(string aliasLower, CancellationToken ct = default)
    {
        if (!IsOutfitOwnedByTeam(aliasLower, out Team? owningTeam))
            return false;

        Outfit? outfit = owningTeam.Outfits.FirstOrDefault(o => o.AliasLower == aliasLower);
        if (outfit is null)
            return false;

        TeamDefinition? teamOrdinal = outfit.TeamOrdinal;

        bool success = RemoveOutfitFromTeam(aliasLower);
        if (!success)
            return false;

        if (teamOrdinal.HasValue)
            await RemoveOutfitMatchDataFromDbAsync(outfit.Id, teamOrdinal.Value, ct);
        await TryUpdateAllTeamMatchResultsInDbAsync(ct);
        await UpdateMatchParticipatingPlayersAsync(ct);

        return true;
    }

    public bool RemoveOutfitFromTeam(string aliasLower)
    {
        if (!IsOutfitOwnedByTeam(aliasLower, out Team? team))
            return false;

        Outfit? outfit = team.Outfits.FirstOrDefault(o => o.AliasLower == aliasLower);
        if(!team.TryRemoveOutfit(aliasLower))
            return false;

        IEnumerable<Player> players = team.Players.Where(p => p.OutfitAliasLower == aliasLower && !p.IsOutfitless);
        bool anyPlayersRemoved = false;

        foreach (Player player in players)
        {
            if (RemovePlayerFromTeam(player))
                anyPlayersRemoved = true;
        }

        //TODO: handle updating Match Configuration's Server ID setting here
        if (team.ConstructedTeamsMatchInfo.Any())
        {
            ConstructedTeamMatchInfo? nextTeam = team.ConstructedTeamsMatchInfo.FirstOrDefault();
            UpdateTeamAlias(team.TeamOrdinal, nextTeam.ConstructedTeam.Alias);
            UpdateTeamFaction(team.TeamOrdinal, nextTeam.ActiveFactionId);
        }
        else if (team.Outfits.Any())
        {
            Outfit? nextOutfit = team.Outfits.FirstOrDefault();
            UpdateTeamAlias(team.TeamOrdinal, nextOutfit.Alias);
            UpdateTeamFaction(team.TeamOrdinal, nextOutfit.FactionId);
        }
        else if (team.Players.Any())
        {
            Player? nextPlayer = team.Players.FirstOrDefault();
            UpdateTeamAlias(team.TeamOrdinal, $"{DEFAULT_ALIAS_PRE_TEXT}{team.TeamOrdinal}");
            UpdateTeamFaction(team.TeamOrdinal, nextPlayer.FactionId);
        }
        else
        {
            UpdateTeamAlias(team.TeamOrdinal, $"{DEFAULT_ALIAS_PRE_TEXT}{team.TeamOrdinal}");
            UpdateTeamFaction(team.TeamOrdinal, null);
        }

        SendTeamOutfitRemovedMessage(outfit);

        return anyPlayersRemoved;
    }

    private async Task RemoveOutfitMatchDataFromDbAsync(ulong outfitId, TeamDefinition teamOrdinal, CancellationToken ct)
    {
        string currentMatchId = _matchDataService.CurrentMatchId;

        if (string.IsNullOrWhiteSpace(currentMatchId))
        {
            return;
        }

        try
        {
            await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

            List<ScrimMatchParticipatingPlayer> participatingPlayers = await dbContext.ScrimMatchParticipatingPlayers
                .Where(e => e.ScrimMatchId == currentMatchId
                    && e.TeamOrdinal == teamOrdinal
                    && e.IsFromOutfit
                    && e.OutfitId == outfitId)
                .ToListAsync(ct);

            // TODO: can a TaskList be used safely for this?
            foreach (ScrimMatchParticipatingPlayer player in participatingPlayers)
            {
                await RemoveCharacterMatchDataFromDbAsync(player.CharacterId, teamOrdinal, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove an outfit's match data from the database");
        }
    }

    public async Task<bool> RemoveConstructedTeamFactionFromTeamAndDbAsync
    (
        int constructedTeamId,
        FactionDefinition factionId,
        CancellationToken ct = default
    )
    {
        bool success = RemoveConstructedTeamFactionFromTeam(constructedTeamId, factionId);

        if (!success)
        {
            return false;
        }

        List<Task> TaskList = new();

        await RemoveConstructedTeamFactionMatchDataFromDbAsync(constructedTeamId, factionId, ct);

        Task updateTeamResultsToDbTask = TryUpdateAllTeamMatchResultsInDbAsync(ct);
        TaskList.Add(updateTeamResultsToDbTask);

        await Task.WhenAll(TaskList);

        await UpdateMatchParticipatingPlayersAsync(ct);

        return true;
    }

    public bool RemoveConstructedTeamFactionFromTeam(int constructedTeamId, FactionDefinition factionId)
    {
        Team? team = GetTeamFromConstructedTeamFaction(constructedTeamId, factionId);

        if (team == null)
        {
            return false;
        }

        ConstructedTeamMatchInfo? constructedTeamMatchInfo = team.ConstructedTeamsMatchInfo
            .FirstOrDefault(t => t.ConstructedTeam?.Id == constructedTeamId && t.ActiveFactionId == factionId);

        if (!team.TryRemoveConstructedTeamFaction(constructedTeamId, factionId))
        {
            return false;
        }

        List<Player>? players = team.GetConstructedTeamFactionPlayers(constructedTeamId, factionId).ToList();

        bool anyPlayersRemoved = false;

        if (players != null && players.Any())
        {
            foreach (Player player in players)
            {
                if (RemovePlayerFromTeam(player))
                {
                    anyPlayersRemoved = true;
                }
            }
        }

        //TODO: handle updating Match Configuration's Server ID (World ID) setting here
        if (team.ConstructedTeamsMatchInfo.Any())
        {
            ConstructedTeamMatchInfo? nextTeam = team.ConstructedTeamsMatchInfo.FirstOrDefault();
            UpdateTeamAlias(team.TeamOrdinal, nextTeam.ConstructedTeam.Alias);
            UpdateTeamFaction(team.TeamOrdinal, nextTeam.ActiveFactionId);
        }
        else if (team.Outfits.Any())
        {
            Outfit? nextOutfit = team.Outfits.FirstOrDefault();
            UpdateTeamAlias(team.TeamOrdinal, nextOutfit.Alias);
            UpdateTeamFaction(team.TeamOrdinal, nextOutfit.FactionId);
        }
        else if (team.Players.Any())
        {
            Player? nextPlayer = team.Players.FirstOrDefault();
            UpdateTeamAlias(team.TeamOrdinal, $"{DEFAULT_ALIAS_PRE_TEXT}{team.TeamOrdinal}");
            UpdateTeamFaction(team.TeamOrdinal, nextPlayer.FactionId);
        }
        else
        {
            UpdateTeamAlias(team.TeamOrdinal, $"{DEFAULT_ALIAS_PRE_TEXT}{team.TeamOrdinal}");
            UpdateTeamFaction(team.TeamOrdinal, null);
        }

        SendTeamConstructedTeamRemovedMessage(team.TeamOrdinal, constructedTeamMatchInfo);

        return anyPlayersRemoved;
    }

    private async Task RemoveConstructedTeamFactionMatchDataFromDbAsync
    (
        int constructedTeamId,
        FactionDefinition factionId,
        CancellationToken ct
    )
    {
        Team? team = GetTeamFromConstructedTeamFaction(constructedTeamId, factionId);

        if (team == null)
        {
            return;
        }

        TeamDefinition teamOrdinal = team.TeamOrdinal;

        string currentMatchId = _matchDataService.CurrentMatchId;

        if (string.IsNullOrWhiteSpace(currentMatchId))
        {
            return;
        }

        try
        {
            await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

            List<ScrimMatchParticipatingPlayer> participatingPlayers = await dbContext.ScrimMatchParticipatingPlayers
                .Where(e => e.ScrimMatchId == currentMatchId
                    && e.IsFromConstructedTeam
                    && e.ConstructedTeamId == constructedTeamId
                    && e.FactionId == factionId)
                .ToListAsync(ct);

            // TODO: can a TaskList be used safely for this?
            foreach (ScrimMatchParticipatingPlayer player in participatingPlayers)
            {
                await RemoveCharacterMatchDataFromDbAsync(player.CharacterId, teamOrdinal, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove a constructed team's match data, given its faction ID");
        }
    }

    public async Task<bool> RemoveCharacterFromTeamAndDbAsync(ulong characterId, CancellationToken ct = default)
    {
        TeamDefinition? teamOrdinal = GetTeamOrdinalFromPlayerId(characterId);
        if (teamOrdinal is null)
            return false;

        bool success = RemoveCharacterFromTeam(characterId);

        if (!success)
        {
            return false;
        }

        List<Task> TaskList = new();

        await RemoveCharacterMatchDataFromDbAsync(characterId, teamOrdinal.Value, ct);

        Task updateTeamResultsToDbTask = TryUpdateAllTeamMatchResultsInDbAsync(ct);
        TaskList.Add(updateTeamResultsToDbTask);

        await Task.WhenAll(TaskList);

        await UpdateMatchParticipatingPlayersAsync(ct);

        return true;
    }

    private async Task RemoveCharacterMatchDataFromDbAsync
    (
        ulong characterId,
        TeamDefinition teamOrdinal,
        CancellationToken ct
    )
    {
        List<Task> TaskList = new();

        Task deathsTask = RemoveCharacterMatchDeathsFromDbAsync(characterId, teamOrdinal, ct);
        TaskList.Add(deathsTask);

        Task destructionsTask = RemoveCharacterMatchVehicleDestructionsFromDbAsync(characterId, ct);
        TaskList.Add(destructionsTask);

        Task revivesTask = RemoveCharacterMatchRevivesFromDbAsync(characterId, teamOrdinal, ct);
        TaskList.Add(revivesTask);

        Task damageAssistsTask = RemoveCharacterMatchDamageAssistsFromDbAsync(characterId, teamOrdinal, ct);
        TaskList.Add(damageAssistsTask);

        Task grenadeAssistsTask = RemoveCharacterMatchGrenadeAssistsFromDbAsync(characterId, teamOrdinal, ct);
        TaskList.Add(grenadeAssistsTask);

        Task spotAssistsTask = RemoveCharacterMatchSpotAssistsFromDbAsync(characterId, teamOrdinal, ct);
        TaskList.Add(spotAssistsTask);

        await Task.WhenAll(TaskList);

        await _matchDataService.TryRemoveMatchParticipatingPlayerAsync(characterId, ct); // TODO: add this to TaskList?
    }

    #region Remove Character Match Events From DB
    private async Task RemoveCharacterMatchDeathsFromDbAsync
    (
        ulong characterId,
        TeamDefinition teamOrdinal,
        CancellationToken ct
    )
    {
        string currentMatchId = _matchDataService.CurrentMatchId;
        int currentMatchRound = _matchDataService.CurrentMatchRound;

        if (currentMatchRound <= 0)
        {
            return;
        }

        using (await _characterMatchDataLock.WaitAsync("Deaths", ct))
        {
            try
            {
                await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

                List<ScrimDeath>? allDeathEvents = await dbContext.ScrimDeaths
                    .Where(e => e.ScrimMatchId == currentMatchId
                        && (e.AttackerCharacterId == characterId
                            || e.VictimCharacterId == characterId))
                    .ToListAsync(ct);

                if (allDeathEvents.Count is 0)
                    return;

                #region Set Up Distinct Interaction Target Lists
                List<TeamDefinition> distinctVictimTeams = allDeathEvents
                    .Where(e => e.AttackerCharacterId == characterId && e.VictimTeamOrdinal.HasValue)
                    .Select(e => e.VictimTeamOrdinal!.Value)
                    .Distinct()
                    .ToList();

                List<TeamDefinition> distinctAttackerTeams = allDeathEvents
                    .Where(e => e.VictimCharacterId == characterId && e.AttackerTeamOrdinal.HasValue)
                    .Select(e => e.AttackerTeamOrdinal!.Value)
                    .Distinct()
                    .ToList();

                List<TeamDefinition> distinctTeams = new() { teamOrdinal };
                distinctTeams.AddRange(distinctAttackerTeams.Where(e => !distinctTeams.Contains(e)).ToList());
                distinctTeams.AddRange(distinctVictimTeams.Where(e => !distinctTeams.Contains(e)).ToList());


                List<ulong> distinctVictimCharacterIds = allDeathEvents
                    .Where(e => e.AttackerCharacterId == characterId)
                    .Select(e => e.VictimCharacterId)
                    .Distinct()
                    .ToList();

                List<ulong> distinctAttackerCharacterIds = allDeathEvents
                    .Where(e => e.VictimCharacterId == characterId && e.AttackerCharacterId.HasValue)
                    .Select(e => e.AttackerCharacterId!.Value)
                    .Distinct()
                    .ToList();

                List<ulong> distinctCharacterIds = new();
                distinctCharacterIds.AddRange(distinctAttackerCharacterIds);
                distinctCharacterIds.AddRange(distinctVictimCharacterIds.Where(e => !distinctCharacterIds.Contains(e)).ToList());
                #endregion Set Up Distinct Interaction Target Lists

                Dictionary<TeamDefinition, ScrimEventAggregateRoundTracker> teamUpdates = new();
                Dictionary<ulong, ScrimEventAggregateRoundTracker> playerUpdates = new();

                foreach (TeamDefinition team in distinctTeams)
                {
                    ScrimEventAggregateRoundTracker tracker = new();
                    teamUpdates.Add(team, tracker);
                }

                foreach (ulong character in distinctCharacterIds)
                {
                    ScrimEventAggregateRoundTracker tracker = new();
                    playerUpdates.Add(character, tracker);
                }

                if (!playerUpdates.ContainsKey(characterId))
                {
                    playerUpdates.Add(characterId, new ScrimEventAggregateRoundTracker());
                }

                for (int round = 1; round <= currentMatchRound; round++)
                {
                    foreach (ScrimDeath deathEvent in allDeathEvents.Where(e => e.ScrimMatchRound == round))
                    {
                        ulong? attackerId = deathEvent.AttackerCharacterId;
                        ulong? victimId = deathEvent.VictimCharacterId;
                        TeamDefinition? attackerTeamOrdinal = deathEvent.AttackerTeamOrdinal;
                        TeamDefinition? victimTeamOrdinal = deathEvent.VictimTeamOrdinal;
                        DeathEventType deathType = deathEvent.DeathType;
                        int points = deathEvent.Points;
                        int isHeadshot = deathEvent.IsHeadshot ? 1 : 0;

                        if (deathType == DeathEventType.Kill)
                        {
                            ScrimEventAggregate attackerUpdate = new()
                            {
                                Points = points,
                                NetScore = points,
                                Kills = 1,
                                Headshots = isHeadshot
                            };

                            ScrimEventAggregate victimUpdate = new()
                            {
                                NetScore = -points,
                                Deaths = 1,
                                HeadshotDeaths = isHeadshot
                            };

                            teamUpdates[attackerTeamOrdinal.Value].AddToCurrent(attackerUpdate);
                            playerUpdates[attackerId.Value].AddToCurrent(attackerUpdate);

                            teamUpdates[victimTeamOrdinal.Value].AddToCurrent(victimUpdate);
                            playerUpdates[victimId.Value].AddToCurrent(victimUpdate);
                        }
                        else if (deathType == DeathEventType.Suicide)
                        {
                            ScrimEventAggregate victimUpdate = new()
                            {
                                Points = points,
                                NetScore = points,
                                Deaths = 1,
                                Suicides = 1
                            };

                            teamUpdates[victimTeamOrdinal.Value].AddToCurrent(victimUpdate);
                            playerUpdates[victimId.Value].AddToCurrent(victimUpdate);
                        }
                        else if (deathType == DeathEventType.Teamkill)
                        {
                            ScrimEventAggregate attackerUpdate = new()
                            {
                                Points = points,
                                NetScore = points,
                                Teamkills = 1
                            };

                            ScrimEventAggregate victimUpdate = new()
                            {
                                Deaths = 1,
                                TeamkillDeaths = 1
                            };

                            teamUpdates[attackerTeamOrdinal.Value].AddToCurrent(attackerUpdate);
                            playerUpdates[attackerId.Value].AddToCurrent(attackerUpdate);

                            teamUpdates[victimTeamOrdinal.Value].AddToCurrent(victimUpdate);
                            playerUpdates[victimId.Value].AddToCurrent(victimUpdate);
                        }
                        else
                        {
                            continue;
                        }
                    }

                    foreach (TeamDefinition team in distinctTeams)
                    {
                        teamUpdates[team].SaveRoundToHistory(round);
                    }

                    foreach (ulong character in distinctCharacterIds)
                    {
                        playerUpdates[character].SaveRoundToHistory(round);
                    }
                }

                // Transfer the updates to the actual entities
                foreach (TeamDefinition tOrdinal in distinctTeams)
                {
                    Team team = GetTeam(tOrdinal);
                    team.EventAggregateTracker.SubtractFromHistory(teamUpdates[tOrdinal]);

                    SendTeamStatUpdateMessage(team);
                }

                foreach (ulong character in distinctCharacterIds)
                {
                    Player? player = GetPlayerFromId(character);

                    if (player == null)
                    {
                        continue;
                    }

                    player.EventAggregateTracker.SubtractFromHistory(playerUpdates[character]);

                    SendPlayerStatUpdateMessage(player);
                }

                dbContext.ScrimDeaths.RemoveRange(allDeathEvents);

                await dbContext.SaveChangesAsync(ct);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }
    }

    private async Task RemoveCharacterMatchVehicleDestructionsFromDbAsync(ulong characterId, CancellationToken ct)
    {
        string currentMatchId = _matchDataService.CurrentMatchId;

        using (await _characterMatchDataLock.WaitAsync("Destructions", ct))
        {
            try
            {
                await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

                List<ScrimVehicleDestruction> destructionsToRemove = await dbContext.ScrimVehicleDestructions
                    .Where(e => e.ScrimMatchId == currentMatchId
                        && (e.AttackerCharacterId == characterId
                            || e.VictimCharacterId == characterId))
                    .ToListAsync(ct);

                dbContext.ScrimVehicleDestructions.RemoveRange(destructionsToRemove);

                await dbContext.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove a character's vehicle destructions from DB");
            }
        }
    }

    private async Task RemoveCharacterMatchRevivesFromDbAsync
    (
        ulong characterId,
        TeamDefinition teamOrdinal,
        CancellationToken ct
    )
    {
        string currentMatchId = _matchDataService.CurrentMatchId;
        int currentMatchRound = _matchDataService.CurrentMatchRound;

        if (currentMatchRound <= 0)
        {
            return;
        }

        using (await _characterMatchDataLock.WaitAsync("Revives", ct))
        {
            try
            {
                await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

                List<ScrimRevive> allReviveEvents = await dbContext.ScrimRevives
                    .Where
                    (
                        e => e.ScrimMatchId == currentMatchId
                            && (e.MedicCharacterId == characterId || e.RevivedCharacterId == characterId)
                    )
                    .ToListAsync(ct);

                if (allReviveEvents.Count is 0)
                    return;

                #region Set Up Distinct Interaction Target Lists
                List<TeamDefinition> distinctRevivedTeams = allReviveEvents
                    .Where(e => e.MedicCharacterId == characterId && e.RevivedTeamOrdinal.HasValue)
                    .Select(e => e.RevivedTeamOrdinal!.Value)
                    .Distinct()
                    .ToList();

                List<TeamDefinition> distinctMedicTeams = allReviveEvents
                    .Where(e => e.RevivedCharacterId == characterId && e.MedicTeamOrdinal.HasValue)
                    .Select(e => e.MedicTeamOrdinal!.Value)
                    .Distinct()
                    .ToList();

                List<TeamDefinition> distinctTeams = new() { teamOrdinal };
                distinctTeams.AddRange(distinctMedicTeams.Where(e => !distinctTeams.Contains(e)).ToList());
                distinctTeams.AddRange(distinctRevivedTeams.Where(e => !distinctTeams.Contains(e)).ToList());

                List<ulong> distinctRevivedCharacterIds = allReviveEvents
                    .Where(e => e.MedicCharacterId == characterId)
                    .Select(e => e.RevivedCharacterId)
                    .Distinct()
                    .ToList();

                List<ulong> distinctMedicCharacterIds = allReviveEvents
                    .Where(e => e.RevivedCharacterId == characterId)
                    .Select(e => e.MedicCharacterId)
                    .Distinct()
                    .ToList();

                List<ulong> distinctCharacterIds = new();
                distinctCharacterIds.AddRange(distinctMedicCharacterIds);
                distinctCharacterIds.AddRange(distinctRevivedCharacterIds.Where(e => !distinctCharacterIds.Contains(e)).ToList());
                #endregion Set Up Distinct Interaction Target Lists

                Dictionary<TeamDefinition, ScrimEventAggregateRoundTracker> teamUpdates = new();
                Dictionary<ulong, ScrimEventAggregateRoundTracker> playerUpdates = new();

                foreach (TeamDefinition team in distinctTeams)
                {
                    ScrimEventAggregateRoundTracker tracker = new();
                    teamUpdates.Add(team, tracker);
                }

                foreach (ulong character in distinctCharacterIds)
                {
                    ScrimEventAggregateRoundTracker tracker = new();
                    playerUpdates.Add(character, tracker);
                }

                if (!playerUpdates.ContainsKey(characterId))
                {
                    playerUpdates.Add(characterId, new ScrimEventAggregateRoundTracker());
                }

                for (int round = 1; round <= currentMatchRound; round++)
                {
                    foreach (ScrimRevive reviveEvent in allReviveEvents.Where(e => e.ScrimMatchRound == round))
                    {
                        ulong? medicId = reviveEvent.MedicCharacterId;
                        ulong? revivedId = reviveEvent.RevivedCharacterId;
                        TeamDefinition? medicTeamOrdinal = reviveEvent.MedicTeamOrdinal;
                        TeamDefinition? revivedTeamOrdinal = reviveEvent.RevivedTeamOrdinal;
                        int points = reviveEvent.Points;

                        ScrimEventAggregate medicUpdate = new()
                        {
                            Points = points,
                            NetScore = points,
                            RevivesGiven = 1
                        };

                        ScrimEventAggregate revivedUpdate = new()
                        {
                            NetScore = -points,
                            RevivesTaken = 1
                        };

                        teamUpdates[medicTeamOrdinal.Value].AddToCurrent(medicUpdate);
                        playerUpdates[medicId.Value].AddToCurrent(medicUpdate);

                        teamUpdates[revivedTeamOrdinal.Value].AddToCurrent(revivedUpdate);
                        playerUpdates[revivedId.Value].AddToCurrent(revivedUpdate);
                    }

                    foreach (TeamDefinition team in distinctTeams)
                    {
                        teamUpdates[team].SaveRoundToHistory(round);
                    }

                    foreach (ulong character in distinctCharacterIds)
                    {
                        playerUpdates[character].SaveRoundToHistory(round);
                    }
                }

                // Transfer the updates to the actual entities
                foreach (TeamDefinition tOrdinal in distinctTeams)
                {
                    Team team = GetTeam(tOrdinal);
                    team.EventAggregateTracker.SubtractFromHistory(teamUpdates[tOrdinal]);

                    SendTeamStatUpdateMessage(team);
                }

                foreach (ulong character in distinctCharacterIds)
                {
                    Player? player = GetPlayerFromId(character);

                    if (player == null)
                    {
                        continue;
                    }

                    player.EventAggregateTracker.SubtractFromHistory(playerUpdates[character]);

                    SendPlayerStatUpdateMessage(player);
                }

                dbContext.ScrimRevives.RemoveRange(allReviveEvents);

                await dbContext.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }
    }

    private async Task RemoveCharacterMatchDamageAssistsFromDbAsync
    (
        ulong characterId,
        TeamDefinition teamOrdinal,
        CancellationToken ct
    )
    {
        string currentMatchId = _matchDataService.CurrentMatchId;
        int currentMatchRound = _matchDataService.CurrentMatchRound;

        if (currentMatchRound <= 0)
        {
            return;
        }

        using (await _characterMatchDataLock.WaitAsync("Damages", ct))
        {
            try
            {
                await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

                List<ScrimDamageAssist>? allDamageAssistEvents = await dbContext.ScrimDamageAssists
                    .Where(e => e.ScrimMatchId == currentMatchId
                        && (e.AttackerCharacterId == characterId
                            || e.VictimCharacterId == characterId))
                    .ToListAsync(ct);

                if (allDamageAssistEvents.Count is 0)
                {
                    return;
                }

                #region Set Up Distinct Interaction Target Lists
                List<TeamDefinition> distinctVictimTeams = allDamageAssistEvents
                    .Where(e => e.AttackerCharacterId == characterId && e.VictimTeamOrdinal.HasValue)
                    .Select(e => e.VictimTeamOrdinal!.Value)
                    .Distinct()
                    .ToList();

                List<TeamDefinition> distinctAttackerTeams = allDamageAssistEvents
                    .Where(e => e.VictimCharacterId == characterId && e.AttackerTeamOrdinal.HasValue)
                    .Select(e => e.AttackerTeamOrdinal!.Value)
                    .Distinct()
                    .ToList();

                List<TeamDefinition> distinctTeams = new() { teamOrdinal };
                distinctTeams.AddRange(distinctAttackerTeams.Where(e => !distinctTeams.Contains(e)).ToList());
                distinctTeams.AddRange(distinctVictimTeams.Where(e => !distinctTeams.Contains(e)).ToList());


                List<ulong> distinctVictimCharacterIds = allDamageAssistEvents
                    .Where(e => e.AttackerCharacterId == characterId && e.VictimCharacterId.HasValue)
                    .Select(e => e.VictimCharacterId!.Value)
                    .Distinct()
                    .ToList();

                List<ulong> distinctAttackerCharacterIds = allDamageAssistEvents
                    .Where(e => e.VictimCharacterId == characterId && e.AttackerCharacterId.HasValue)
                    .Select(e => e.AttackerCharacterId!.Value)
                    .Distinct()
                    .ToList();

                List<ulong> distinctCharacterIds = new();
                distinctCharacterIds.AddRange(distinctAttackerCharacterIds);
                distinctCharacterIds.AddRange(distinctVictimCharacterIds.Where(e => !distinctCharacterIds.Contains(e)).ToList());
                #endregion Set Up Distinct Interaction Target Lists

                Dictionary<TeamDefinition, ScrimEventAggregateRoundTracker> teamUpdates = new();
                Dictionary<ulong, ScrimEventAggregateRoundTracker> playerUpdates = new();

                foreach (TeamDefinition team in distinctTeams)
                {
                    ScrimEventAggregateRoundTracker tracker = new();
                    teamUpdates.Add(team, tracker);
                }

                foreach (ulong character in distinctCharacterIds)
                {
                    ScrimEventAggregateRoundTracker tracker = new();
                    playerUpdates.Add(character, tracker);
                }

                if (!playerUpdates.ContainsKey(characterId))
                {
                    playerUpdates.Add(characterId, new ScrimEventAggregateRoundTracker());
                }

                for (int round = 1; round <= currentMatchRound; round++)
                {
                    foreach (ScrimDamageAssist damageAssistEvent in allDamageAssistEvents.Where(e => e.ScrimMatchRound == round))
                    {
                        ulong? attackerId = damageAssistEvent.AttackerCharacterId;
                        ulong? victimId = damageAssistEvent.VictimCharacterId;

                        TeamDefinition? attackerTeamOrdinal = damageAssistEvent.AttackerTeamOrdinal;
                        TeamDefinition? victimTeamOrdinal = damageAssistEvent.VictimTeamOrdinal;

                        int points = damageAssistEvent.Points;

                        ScrimEventAggregate attackerUpdate;
                        ScrimEventAggregate victimUpdate;

                        if (damageAssistEvent.ActionType == ScrimActionType.DamageAssist)
                        {
                            attackerUpdate = new ScrimEventAggregate()
                            {
                                Points = points,
                                NetScore = points,
                                DamageAssists = 1
                            };

                            victimUpdate = new ScrimEventAggregate()
                            {
                                NetScore = -points,
                                DamageAssistedDeaths = 1
                            };
                        }
                        else if (damageAssistEvent.ActionType == ScrimActionType.DamageTeamAssist)
                        {
                            attackerUpdate = new ScrimEventAggregate()
                            {
                                Points = points,
                                NetScore = points,
                                DamageTeamAssists = 1
                            };

                            victimUpdate = new ScrimEventAggregate()
                            {
                                NetScore = -points,
                                DamageAssistedDeaths = 1,
                                DamageTeamAssistedDeaths = 1
                            };
                        }
                        else
                        {
                            continue;
                        }

                        teamUpdates[attackerTeamOrdinal.Value].AddToCurrent(attackerUpdate);
                        playerUpdates[attackerId.Value].AddToCurrent(attackerUpdate);

                        teamUpdates[victimTeamOrdinal.Value].AddToCurrent(victimUpdate);
                        playerUpdates[victimId.Value].AddToCurrent(victimUpdate);
                    }

                    foreach (TeamDefinition team in distinctTeams)
                    {
                        teamUpdates[team].SaveRoundToHistory(round);
                    }

                    foreach (ulong character in distinctCharacterIds)
                    {
                        playerUpdates[character].SaveRoundToHistory(round);
                    }
                }

                // Transfer the updates to the actual entities
                foreach (TeamDefinition tOrdinal in distinctTeams)
                {
                    Team team = GetTeam(tOrdinal);
                    team.EventAggregateTracker.SubtractFromHistory(teamUpdates[tOrdinal]);

                    SendTeamStatUpdateMessage(team);
                }

                foreach (ulong character in distinctCharacterIds)
                {
                    Player? player = GetPlayerFromId(character);

                    if (player == null)
                    {
                        continue;
                    }

                    player.EventAggregateTracker.SubtractFromHistory(playerUpdates[character]);

                    SendPlayerStatUpdateMessage(player);
                }

                dbContext.ScrimDamageAssists.RemoveRange(allDamageAssistEvents);

                await dbContext.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }
    }

    private async Task RemoveCharacterMatchGrenadeAssistsFromDbAsync
    (
        ulong characterId,
        TeamDefinition teamOrdinal,
        CancellationToken ct
    )
    {
        string currentMatchId = _matchDataService.CurrentMatchId;
        int currentMatchRound = _matchDataService.CurrentMatchRound;

        if (currentMatchRound <= 0)
        {
            return;
        }

        using (await _characterMatchDataLock.WaitAsync("Grenades", ct))
        {
            try
            {
                await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

                List<ScrimGrenadeAssist>? allGrenadeAssistEvents = await dbContext.ScrimGrenadeAssists
                    .Where(e => e.ScrimMatchId == currentMatchId
                        && (e.AttackerCharacterId == characterId
                            || e.VictimCharacterId == characterId))
                    .ToListAsync(ct);

                if (allGrenadeAssistEvents.Count is 0)
                {
                    return;
                }

                #region Set Up Distinct Interaction Target Lists
                List<TeamDefinition> distinctVictimTeams = allGrenadeAssistEvents
                    .Where(e => e.AttackerCharacterId == characterId && e.VictimTeamOrdinal.HasValue)
                    .Select(e => e.VictimTeamOrdinal!.Value)
                    .Distinct()
                    .ToList();

                List<TeamDefinition> distinctAttackerTeams = allGrenadeAssistEvents
                    .Where(e => e.VictimCharacterId == characterId && e.AttackerTeamOrdinal.HasValue)
                    .Select(e => e.AttackerTeamOrdinal!.Value)
                    .Distinct()
                    .ToList();

                List<TeamDefinition> distinctTeams = new() { teamOrdinal };
                distinctTeams.AddRange(distinctAttackerTeams.Where(e => !distinctTeams.Contains(e)).ToList());
                distinctTeams.AddRange(distinctVictimTeams.Where(e => !distinctTeams.Contains(e)).ToList());


                List<ulong> distinctVictimCharacterIds = allGrenadeAssistEvents
                    .Where(e => e.AttackerCharacterId == characterId && e.VictimCharacterId.HasValue)
                    .Select(e => e.VictimCharacterId!.Value)
                    .Distinct()
                    .ToList();

                List<ulong> distinctAttackerCharacterIds = allGrenadeAssistEvents
                    .Where(e => e.VictimCharacterId == characterId && e.AttackerCharacterId.HasValue)
                    .Select(e => e.AttackerCharacterId!.Value)
                    .Distinct()
                    .ToList();

                List<ulong> distinctCharacterIds = new();
                distinctCharacterIds.AddRange(distinctAttackerCharacterIds);
                distinctCharacterIds.AddRange(distinctVictimCharacterIds.Where(e => !distinctCharacterIds.Contains(e)).ToList());
                #endregion Set Up Distinct Interaction Target Lists

                Dictionary<TeamDefinition, ScrimEventAggregateRoundTracker> teamUpdates = new();
                Dictionary<ulong, ScrimEventAggregateRoundTracker> playerUpdates = new();

                foreach (TeamDefinition team in distinctTeams)
                {
                    ScrimEventAggregateRoundTracker tracker = new();
                    teamUpdates.Add(team, tracker);
                }

                foreach (ulong character in distinctCharacterIds)
                {
                    ScrimEventAggregateRoundTracker tracker = new();
                    playerUpdates.Add(character, tracker);
                }

                if (!playerUpdates.ContainsKey(characterId))
                {
                    playerUpdates.Add(characterId, new ScrimEventAggregateRoundTracker());
                }

                for (int round = 1; round <= currentMatchRound; round++)
                {
                    foreach (ScrimGrenadeAssist grenadeAssistEvent in allGrenadeAssistEvents.Where(e => e.ScrimMatchRound == round))
                    {
                        ulong? attackerId = grenadeAssistEvent.AttackerCharacterId;
                        ulong? victimId = grenadeAssistEvent.VictimCharacterId;

                        TeamDefinition? attackerTeamOrdinal = grenadeAssistEvent.AttackerTeamOrdinal;
                        TeamDefinition? victimTeamOrdinal = grenadeAssistEvent.VictimTeamOrdinal;

                        int points = grenadeAssistEvent.Points;
                        ScrimEventAggregate attackerUpdate;
                        ScrimEventAggregate victimUpdate;

                        if (grenadeAssistEvent.ActionType == ScrimActionType.GrenadeAssist)
                        {
                            attackerUpdate = new ScrimEventAggregate()
                            {
                                Points = points,
                                NetScore = points,
                                GrenadeAssists = 1
                            };

                            victimUpdate = new ScrimEventAggregate()
                            {
                                NetScore = -points,
                                GrenadeAssistedDeaths = 1
                            };
                        }
                        else if (grenadeAssistEvent.ActionType == ScrimActionType.GrenadeTeamAssist)
                        {
                            attackerUpdate = new ScrimEventAggregate()
                            {
                                Points = points,
                                NetScore = points,
                                GrenadeTeamAssists = 1
                            };

                            victimUpdate = new ScrimEventAggregate()
                            {
                                NetScore = -points,
                                GrenadeAssistedDeaths = 1,
                                GrenadeTeamAssistedDeaths = 1
                            };
                        }
                        else
                        {
                            continue;
                        }

                        teamUpdates[attackerTeamOrdinal.Value].AddToCurrent(attackerUpdate);
                        playerUpdates[attackerId.Value].AddToCurrent(attackerUpdate);

                        teamUpdates[victimTeamOrdinal.Value].AddToCurrent(victimUpdate);
                        playerUpdates[victimId.Value].AddToCurrent(victimUpdate);
                    }

                    foreach (TeamDefinition team in distinctTeams)
                        teamUpdates[team].SaveRoundToHistory(round);

                    foreach (ulong character in distinctCharacterIds)
                    {
                        playerUpdates[character].SaveRoundToHistory(round);
                    }
                }

                // Transfer the updates to the actual entities
                foreach (TeamDefinition tOrdinal in distinctTeams)
                {
                    Team team = GetTeam(tOrdinal);
                    team.EventAggregateTracker.SubtractFromHistory(teamUpdates[tOrdinal]);

                    SendTeamStatUpdateMessage(team);
                }

                foreach (ulong character in distinctCharacterIds)
                {
                    Player? player = GetPlayerFromId(character);

                    if (player == null)
                    {
                        continue;
                    }

                    player.EventAggregateTracker.SubtractFromHistory(playerUpdates[character]);

                    SendPlayerStatUpdateMessage(player);
                }

                dbContext.ScrimGrenadeAssists.RemoveRange(allGrenadeAssistEvents);

                await dbContext.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }
    }

    private async Task RemoveCharacterMatchSpotAssistsFromDbAsync
    (
        ulong characterId,
        TeamDefinition teamOrdinal,
        CancellationToken ct
    )
    {
        string currentMatchId = _matchDataService.CurrentMatchId;
        int currentMatchRound = _matchDataService.CurrentMatchRound;

        if (currentMatchRound <= 0)
        {
            return;
        }


        using (await _characterMatchDataLock.WaitAsync("Spots", ct))
        {
            try
            {
                await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

                List<ScrimSpotAssist>? allSpotAssistEvents = await dbContext.ScrimSpotAssists
                    .Where(e => e.ScrimMatchId == currentMatchId
                        && (e.SpotterCharacterId == characterId
                            || e.VictimCharacterId == characterId))
                    .ToListAsync(ct);

                if (allSpotAssistEvents == null || !allSpotAssistEvents.Any())
                {
                    return;
                }

                #region Set Up Distinct Interaction Target Lists
                List<TeamDefinition> distinctVictimTeams = allSpotAssistEvents
                    .Where(e => e.SpotterCharacterId == characterId && e.VictimTeamOrdinal.HasValue)
                    .Select(e => e.VictimTeamOrdinal!.Value)
                    .Distinct()
                    .ToList();

                List<TeamDefinition> distinctSpotterTeams = allSpotAssistEvents
                    .Where(e => e.VictimCharacterId == characterId && e.SpotterTeamOrdinal.HasValue)
                    .Select(e => e.SpotterTeamOrdinal!.Value)
                    .Distinct()
                    .ToList();

                List<TeamDefinition> distinctTeams = new() { teamOrdinal };
                distinctTeams.AddRange(distinctSpotterTeams.Where(e => !distinctTeams.Contains(e)).ToList());
                distinctTeams.AddRange(distinctVictimTeams.Where(e => !distinctTeams.Contains(e)).ToList());


                List<ulong> distinctVictimCharacterIds = allSpotAssistEvents
                    .Where(e => e.SpotterCharacterId == characterId && e.VictimCharacterId.HasValue)
                    .Select(e => e.VictimCharacterId!.Value)
                    .Distinct()
                    .ToList();

                List<ulong> distinctSpotterCharacterIds = allSpotAssistEvents
                    .Where(e => e.VictimCharacterId == characterId && e.SpotterCharacterId.HasValue)
                    .Select(e => e.SpotterCharacterId!.Value)
                    .Distinct()
                    .ToList();

                List<ulong> distinctCharacterIds = new();
                distinctCharacterIds.AddRange(distinctSpotterCharacterIds);
                distinctCharacterIds.AddRange(distinctVictimCharacterIds.Where(e => !distinctCharacterIds.Contains(e)).ToList());
                #endregion Set Up Distinct Interaction Target Lists

                Dictionary<TeamDefinition, ScrimEventAggregateRoundTracker> teamUpdates = new();
                Dictionary<ulong, ScrimEventAggregateRoundTracker> playerUpdates = new();

                foreach (TeamDefinition team in distinctTeams)
                {
                    ScrimEventAggregateRoundTracker tracker = new();
                    teamUpdates.Add(team, tracker);
                }

                foreach (ulong character in distinctCharacterIds)
                {
                    ScrimEventAggregateRoundTracker tracker = new();
                    playerUpdates.Add(character, tracker);
                }

                if (!playerUpdates.ContainsKey(characterId))
                {
                    playerUpdates.Add(characterId, new ScrimEventAggregateRoundTracker());
                }

                for (int round = 1; round <= currentMatchRound; round++)
                {
                    foreach (ScrimSpotAssist spotAssistEvent in allSpotAssistEvents.Where(e => e.ScrimMatchRound == round))
                    {
                        ulong? spotterId = spotAssistEvent.SpotterCharacterId;
                        ulong? victimId = spotAssistEvent.VictimCharacterId;
                        TeamDefinition? spotterTeamOrdinal = spotAssistEvent.SpotterTeamOrdinal;
                        TeamDefinition? victimTeamOrdinal = spotAssistEvent.VictimTeamOrdinal;
                        int points = spotAssistEvent.Points;

                        ScrimEventAggregate spotterUpdate = new()
                        {
                            Points = points,
                            NetScore = points,
                            SpotAssists = 1
                        };

                        ScrimEventAggregate victimUpdate = new()
                        {
                            NetScore = -points,
                            SpotAssistedDeaths = 1
                        };

                        teamUpdates[spotterTeamOrdinal.Value].AddToCurrent(spotterUpdate);
                        playerUpdates[spotterId.Value].AddToCurrent(spotterUpdate);

                        teamUpdates[victimTeamOrdinal.Value].AddToCurrent(victimUpdate);
                        playerUpdates[victimId.Value].AddToCurrent(victimUpdate);
                    }

                    foreach (TeamDefinition team in distinctTeams)
                    {
                        teamUpdates[team].SaveRoundToHistory(round);
                    }

                    foreach (ulong character in distinctCharacterIds)
                    {
                        playerUpdates[character].SaveRoundToHistory(round);
                    }
                }

                // Transfer the updates to the actual entities
                foreach (TeamDefinition tOrdinal in distinctTeams)
                {
                    Team team = GetTeam(tOrdinal);
                    team.EventAggregateTracker.SubtractFromHistory(teamUpdates[tOrdinal]);

                    SendTeamStatUpdateMessage(team);
                }

                foreach (ulong character in distinctCharacterIds)
                {
                    Player? player = GetPlayerFromId(character);

                    if (player == null)
                    {
                        continue;
                    }

                    player.EventAggregateTracker.SubtractFromHistory(playerUpdates[character]);

                    SendPlayerStatUpdateMessage(player);
                }

                dbContext.ScrimSpotAssists.RemoveRange(allSpotAssistEvents);

                await dbContext.SaveChangesAsync(ct);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }
    }
    #endregion Remove Character Match Events From DB

    public bool RemoveCharacterFromTeam(ulong characterId)
    {
        Player? player = GetPlayerFromId(characterId);

        if (player == null)
        {
            return false;
        }

        //string aliasLower = string.Empty;

        //if (!player.IsOutfitless && !player.IsFromConstructedTeam && !string.IsNullOrWhiteSpace(player.OutfitAliasLower))
        //{
        //    aliasLower = player.OutfitAliasLower;
        //}

        if (RemovePlayerFromTeam(player))
        {
            Team team = GetTeam(player.TeamOrdinal);

            if (!player.IsOutfitless && !player.IsFromConstructedTeam && !string.IsNullOrWhiteSpace(player.OutfitAliasLower))
            {
                Outfit? outfit = team.Outfits.Where(o => o.AliasLower == player.OutfitAliasLower).FirstOrDefault();

                if (outfit != null)
                {
                    outfit.MemberCount -= 1;
                    outfit.MembersOnlineCount -= player.IsOnline ? 1 : 0;
                }
            }
            else if (player.IsFromConstructedTeam && player.ConstructedTeamId != null)
            {
                int constructedTeamId = (int)player.ConstructedTeamId;

                ConstructedTeamMatchInfo? constructedTeamMatchInfo = team.ConstructedTeamsMatchInfo.FirstOrDefault(t => t.ConstructedTeam?.Id == constructedTeamId);

                if (constructedTeamMatchInfo != null)
                {
                    constructedTeamMatchInfo.MembersFactionCount -= 1;
                    //constructedTeamMatchInfo.TotalMembersCount -= 1;
                    constructedTeamMatchInfo.MembersOnlineCount -= player.IsOnline ? 1 : 0;
                }
            }

            if (characterId == MaxPlayerPointsTracker.OwningCharacterId)
            {
                // TODO: Update Match Max Player Points
            }

            if (team.ConstructedTeamsMatchInfo.Any())
            {
                ConstructedTeamMatchInfo? nextTeam = team.ConstructedTeamsMatchInfo.FirstOrDefault();
                UpdateTeamFaction(team.TeamOrdinal, nextTeam.ActiveFactionId);
            }
            else if (team.Outfits.Any())
            {
                Outfit? nextOutfit = team.Outfits.FirstOrDefault();
                UpdateTeamFaction(team.TeamOrdinal, nextOutfit.FactionId);
            }
            else if (team.Players.Any())
            {
                Player? nextPlayer = team.Players.FirstOrDefault();
                UpdateTeamFaction(team.TeamOrdinal, nextPlayer.FactionId);
            }
            else
            {
                UpdateTeamFaction(team.TeamOrdinal, null);
            }

            return true;
        }
        else
        {
            return false;
        }
    }

    public bool RemovePlayerFromTeam(Player player)
    {
        Team team = GetTeam(player.TeamOrdinal);

        if(team.TryRemovePlayer(player.Id))
        {
            _allPlayers.RemoveAll(p => p.Id == player.Id);

            _playerTeamMap.TryRemove(player.Id, out _);

            if (!team.Players.Any())
            {
                UpdateTeamFaction(player.TeamOrdinal, null);
            }

            SendTeamPlayerRemovedMessage(player);

            return true;
        }

        return false;
    }

    private async Task UpdateMatchParticipatingPlayersAsync(CancellationToken ct)
    {
        string matchId = _matchDataService.CurrentMatchId;

        try
        {
            await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

            List<ulong> allMatchParticipatingPlayerIds = await dbContext.ScrimMatchParticipatingPlayers
                .Where(e => e.ScrimMatchId == matchId)
                .Select(e => e.CharacterId)
                .ToListAsync();

            if (!allMatchParticipatingPlayerIds.Any())
            {
                return;
            }

            List<Task> TaskList = new();

            foreach (ulong playerId in allMatchParticipatingPlayerIds)
            {
                Player? player = GetPlayerFromId(playerId);

                if (player == null)
                {
                    continue;
                }

                if (!player.EventAggregateTracker.RoundHistory.Any() || player.EventAggregate.Events == 0)
                {
                    Task playerTask = SetPlayerParticipatingStatus(playerId, false);
                    TaskList.Add(playerTask);
                }
            }

            await Task.WhenAll(TaskList);
        }
        catch (Exception ex)
        {
            _logger.LogError($"{ex}");
        }
    }
    #endregion Remove Entities From Teams


    #region Clear Teams
    public void ClearAllTeams()
    {
        foreach (TeamDefinition teamOrdinal in _ordinalTeamMap.Keys.ToList())
        {
            ClearTeam(teamOrdinal);
        }

        MaxPlayerPointsTracker = new MaxPlayerPointsTracker();
    }

    public void ClearTeam(TeamDefinition teamOrdinal)
    {
        Team team = GetTeam(teamOrdinal);
        UnlockTeamPlayers(teamOrdinal);
        _messageService.BroadcastTeamLockStatusChangeMessage(new TeamLockStatusChangeMessage(teamOrdinal, false));

        List<ConstructedTeamMatchInfo> constructedTeamsMatchInfo = team.ConstructedTeamsMatchInfo.ToList();

        foreach(ConstructedTeamMatchInfo matchInfo in constructedTeamsMatchInfo)
        {
            RemoveConstructedTeamFactionFromTeam(matchInfo.ConstructedTeam.Id, matchInfo.ActiveFactionId);
        }

        List<string> allAliases = team.Outfits.Select(o => o.AliasLower).ToList();

        foreach (string alias in allAliases)
        {
            RemoveOutfitFromTeam(alias);
        }

        if (team.Players.Any())
        {
            List<Player> allPlayers = team.Players.ToList();

            foreach (Player player in allPlayers)
            {
                RemovePlayerFromTeam(player);
            }
        }

        team.ClearEventAggregateHistory();
        team.ClearScrimSeriesMatchResults();

        string oldAlias = team.Alias;
        team.ResetAlias($"{DEFAULT_ALIAS_PRE_TEXT}{teamOrdinal}");

        _messageService.BroadcastTeamAliasChangeMessage(new TeamAliasChangeMessage(teamOrdinal, team.Alias, oldAlias));

        // TODO: broadcast "Finished Clearing Team" message
    }
    #endregion Clear Teams

    #region Rematch Handling - Teams' Match Data
    public List<ScrimSeriesMatchResult> GetTeamsScrimSeriesMatchResults(TeamDefinition teamOrdinal)
    {
        List<ScrimSeriesMatchResult> seriesResults = new();

        seriesResults.AddRange(GetTeam(teamOrdinal).ScrimSeriesMatchResults);

        return seriesResults;
    }

    public void UpdateAllTeamsMatchSeriesResults(int seriesMatchNumber)
    {
        TeamDefinition highestScoreTeamOrdinal = 0;
        int highestScoreValue = 0;

        bool isDraw = false;
        List<TeamDefinition> drawTeamOrdinals = new();
        List<TeamDefinition> scoredTeamOrdinals = new();

        foreach (TeamDefinition teamOrdinal in _ordinalTeamMap.Keys)
        {
            int teamScore = GetTeamScoreDisplay(teamOrdinal);
            if (teamScore == null)
            {
                continue;
            }

            int teamScoreInt = teamScore;

            if (!scoredTeamOrdinals.Any())
            {
                highestScoreTeamOrdinal = teamOrdinal;
                highestScoreValue = teamScoreInt;
            }
            else if (teamScoreInt > highestScoreValue)
            {
                highestScoreValue = teamScoreInt;
                highestScoreTeamOrdinal = teamOrdinal;

                isDraw = false;
            }
            else if (teamScoreInt == highestScoreValue)
            {
                if (drawTeamOrdinals.Any())
                {
                    isDraw = true;
                }

                drawTeamOrdinals.Add(teamOrdinal);
            }

            scoredTeamOrdinals.Add(teamOrdinal);
        }

        if (!scoredTeamOrdinals.Any())
        {
            return;
        }

        foreach (TeamDefinition teamOrdinal in scoredTeamOrdinals)
        {
            ScrimSeriesMatchResultType teamMatchResultType;

            if (teamOrdinal == highestScoreTeamOrdinal)
            {
                teamMatchResultType = ScrimSeriesMatchResultType.Win;
            }
            else if (isDraw && drawTeamOrdinals.Contains(teamOrdinal))
            {
                teamMatchResultType = ScrimSeriesMatchResultType.Draw;
            }
            else
            {
                teamMatchResultType = ScrimSeriesMatchResultType.Loss;
            }

            UpdateAllTeamsMatchSeriesResults(teamOrdinal, seriesMatchNumber, teamMatchResultType);
        }
    }

    public void UpdateAllTeamsMatchSeriesResults(TeamDefinition teamOrdinal, int seriesMatchNumber, ScrimSeriesMatchResultType matchResultType)
    {
        Team team = GetTeam(teamOrdinal);
        team.UpdateScrimSeriesMatchResults(seriesMatchNumber, matchResultType);
    }

    public void ResetAllTeamsMatchData()
    {
        MaxPlayerPointsTracker = new MaxPlayerPointsTracker();

        foreach (TeamDefinition teamOrdinal in _ordinalTeamMap.Keys)
        {
            ResetTeamMatchData(teamOrdinal);
        }
    }

    private void ResetTeamMatchData(TeamDefinition teamOrdinal)
    {
        Team team = GetTeam(teamOrdinal);

        OverlayMessageData overlayMessageData = new()
        {
            RedrawPointGraph = true,
            MatchMaxPlayerPoints = MaxPlayerPointsTracker.MaxPoints
        };

        team.ResetMatchData();
        SendTeamStatUpdateMessage(team, overlayMessageData);

        List<Player> allPlayers = team.Players.ToList();
        foreach (Player player in allPlayers)
        {
            player.ResetMatchData();

            SendPlayerStatUpdateMessage(player, overlayMessageData);
        }
    }


    #endregion Reset Teams' Match Data (for Rematch)

    #region Team Locking
    public bool GetTeamLockStatus(TeamDefinition teamOrdinal)
        => GetTeam(teamOrdinal).IsLocked;

    public async Task LockTeamPlayers(TeamDefinition teamOrdinal)
    {
        Team team = GetTeam(teamOrdinal);

        // TODO: add KeyedSemaphoreSlim for each team

        try
        {
            team.IsLocked = true;

            _messageService.BroadcastTeamLockStatusChangeMessage(new TeamLockStatusChangeMessage(teamOrdinal, true));

            List<Player> playersToRemove = team.Players.Where(p => !p.IsVisibleInTeamComposer).ToList();

            Dictionary<Player, Task<bool>> removeTasks = playersToRemove.ToDictionary(p => p, p => RemoveCharacterFromTeamAndDbAsync(p.Id));

            await Task.WhenAll(removeTasks.Values);

            foreach (Outfit outfit in team.Outfits)
            {
                outfit.MemberCount = team.Players.Count(p => p.OutfitAliasLower == outfit.AliasLower && !p.IsOutfitless);
                outfit.MembersOnlineCount = team.Players.Count(p => p.OutfitAliasLower == outfit.AliasLower && p is { IsOutfitless: false, IsOnline: true });

                TeamOutfitChangeMessage loadCompleteMessage = new(outfit, TeamChangeType.OutfitMembersLoadCompleted);
                _messageService.BroadcastTeamOutfitChangeMessage(loadCompleteMessage);
            }

            // TODO: broadcast some other "Team Lock Status Change" message here, too?
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed locking team {Ordinal} players", teamOrdinal);
        }
    }

    public void UnlockTeamPlayers(TeamDefinition teamOrdinal)
    {
        Team team = GetTeam(teamOrdinal);
        team.IsLocked = false;
        _messageService.BroadcastTeamLockStatusChangeMessage(new TeamLockStatusChangeMessage(teamOrdinal, false));
    }


    public void UnlockAllTeamPlayers()
    {
        foreach (TeamDefinition teamOrdinal in _ordinalTeamMap.Keys)
        {
            UnlockTeamPlayers(teamOrdinal);
        }
    }
    #endregion Team Locking

    #region Roll Back Round
    public async Task RollBackAllTeamStatsAsync(int currentRound, CancellationToken ct = default)
    {
        List<Task> TaskList = new();

        foreach (TeamDefinition teamOrdinal in _ordinalTeamMap.Keys.ToList())
        {
            RollBackTeamStats(teamOrdinal, currentRound);

            Task teamTask = SaveTeamMatchResultsToDbAsync(teamOrdinal, ct);
            TaskList.Add(teamTask);
        }

        Task eventsDbTask = RemoveAllMatchRoundEventsFromDbAsync(currentRound, ct);
        TaskList.Add(eventsDbTask);

        Task participatingPlayersTask = UpdateMatchParticipatingPlayersAsync(ct);
        TaskList.Add(participatingPlayersTask);

        await Task.WhenAll(TaskList);
    }

    public void RollBackTeamStats(TeamDefinition teamOrdinal, int currentRound)
    {
        Team team = GetTeam(teamOrdinal);
        team.EventAggregateTracker.RollBackRound(currentRound);

        IEnumerable<Player> players = team.GetParticipatingPlayers();
        foreach (Player player in players)
        {
            player.EventAggregateTracker.RollBackRound(currentRound);
            SendPlayerStatUpdateMessage(player);
        }

        bool maxPointsChanged = TryUpdateMaxPlayerPointsTrackerFromTeam(teamOrdinal);

        OverlayMessageData overlayMessageData = new()
        {
            RedrawPointGraph = maxPointsChanged,
            MatchMaxPlayerPoints = MaxPlayerPointsTracker.MaxPoints
        };

        SendTeamStatUpdateMessage(team, overlayMessageData);
    }

    #region Remove All Match Round Events From DB
    private async Task RemoveAllMatchRoundEventsFromDbAsync(int roundToRemove, CancellationToken ct)
    {
        List<Task> TaskList = new();

        Task deathsTask = RemoveAllMatchRoundDeathsFromDbAsync(roundToRemove, ct);
        TaskList.Add(deathsTask);

        Task destructionsTask = RemoveAllMatchRoundVehicleDestructionsFromDbAsync(roundToRemove, ct);
        TaskList.Add(destructionsTask);

        Task revivesTask = RemoveAllMatchRoundRevivesFromDbAsync(roundToRemove, ct);
        TaskList.Add(revivesTask);

        Task damageAssistsTask = RemoveAllMatchRoundDamageAssistsFromDbAsync(roundToRemove, ct);
        TaskList.Add(damageAssistsTask);

        Task grenadeAssistsTask = RemoveAllMatchRoundGrenadeAssistsFromDbAsync(roundToRemove, ct);
        TaskList.Add(grenadeAssistsTask);

        Task spotAssistsTask = RemoveAllMatchRoundSpotAssistsFromDbAsync(roundToRemove, ct);
        TaskList.Add(spotAssistsTask);

        Task controlsTask = RemoveAllMatchRoundFacilityControlsFromDbAsync(roundToRemove, ct);
        TaskList.Add(controlsTask);

        await Task.WhenAll(TaskList);
    }

    private async Task RemoveAllMatchRoundDeathsFromDbAsync(int roundToRemove, CancellationToken ct)
    {
        string currentMatchId = _matchDataService.CurrentMatchId;

        try
        {
            await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

            IEnumerable<ScrimDeath> allDeathEvents = dbContext.ScrimDeaths
                .Where(e => e.ScrimMatchId == currentMatchId
                    && e.ScrimMatchRound == roundToRemove)
                .AsEnumerable();

            dbContext.ScrimDeaths.RemoveRange(allDeathEvents);

            await dbContext.SaveChangesAsync(ct);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove all match round deaths from DB");
        }
    }

    private async Task RemoveAllMatchRoundVehicleDestructionsFromDbAsync(int roundToRemove, CancellationToken ct)
    {
        string currentMatchId = _matchDataService.CurrentMatchId;

        try
        {
            await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

            IEnumerable<ScrimVehicleDestruction> destructionsToRemove = dbContext.ScrimVehicleDestructions
                .Where(e => e.ScrimMatchId == currentMatchId
                    && e.ScrimMatchRound == roundToRemove)
                .AsEnumerable();

            dbContext.ScrimVehicleDestructions.RemoveRange(destructionsToRemove);

            await dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove all match round vehicle destructions from DB");
        }
    }

    private async Task RemoveAllMatchRoundRevivesFromDbAsync(int roundToRemove, CancellationToken ct)
    {
        string currentMatchId = _matchDataService.CurrentMatchId;

        try
        {
            await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

            IEnumerable<ScrimRevive> revivesToRemove = dbContext.ScrimRevives
                .Where(e => e.ScrimMatchId == currentMatchId
                    && e.ScrimMatchRound == roundToRemove)
                .AsEnumerable();

            dbContext.ScrimRevives.RemoveRange(revivesToRemove);

            await dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.ToString());
        }
    }

    private async Task RemoveAllMatchRoundDamageAssistsFromDbAsync(int roundToRemove, CancellationToken ct)
    {
        string currentMatchId = _matchDataService.CurrentMatchId;

        try
        {
            await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

            IEnumerable<ScrimDamageAssist> damageAssistsToRemove = dbContext.ScrimDamageAssists
                .Where(e => e.ScrimMatchId == currentMatchId
                    && e.ScrimMatchRound == roundToRemove)
                .AsEnumerable();

            dbContext.ScrimDamageAssists.RemoveRange(damageAssistsToRemove);

            await dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.ToString());
        }
    }

    private async Task RemoveAllMatchRoundGrenadeAssistsFromDbAsync(int roundToRemove, CancellationToken ct)
    {
        string currentMatchId = _matchDataService.CurrentMatchId;

        try
        {
            await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

            IEnumerable<ScrimGrenadeAssist> grenadeAssistsToRemove = dbContext.ScrimGrenadeAssists
                .Where(e => e.ScrimMatchId == currentMatchId
                    && e.ScrimMatchRound == roundToRemove)
                .AsEnumerable();

            dbContext.ScrimGrenadeAssists.RemoveRange(grenadeAssistsToRemove);

            await dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.ToString());
        }
    }

    private async Task RemoveAllMatchRoundSpotAssistsFromDbAsync(int roundToRemove, CancellationToken ct)
    {
        string currentMatchId = _matchDataService.CurrentMatchId;

        try
        {
            await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

            IEnumerable<ScrimSpotAssist> spotAssistsToRemove = dbContext.ScrimSpotAssists
                .Where(e => e.ScrimMatchId == currentMatchId
                    && e.ScrimMatchRound == roundToRemove)
                .AsEnumerable();

            dbContext.ScrimSpotAssists.RemoveRange(spotAssistsToRemove);

            await dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.ToString());
        }
    }

    private async Task RemoveAllMatchRoundFacilityControlsFromDbAsync(int roundToRemove, CancellationToken ct)
    {
        string currentMatchId = _matchDataService.CurrentMatchId;

        try
        {
            await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

            IEnumerable<ScrimFacilityControl> allControlEvents = dbContext.ScrimFacilityControls
                .Where(e => e.ScrimMatchId == currentMatchId
                    && e.ScrimMatchRound == roundToRemove)
                .AsEnumerable();

            dbContext.ScrimFacilityControls.RemoveRange(allControlEvents);

            await dbContext.SaveChangesAsync(ct);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex.ToString());
            return;
        }
    }
    #endregion Remove All Match Round Events From DB

    #endregion Roll Back Round

    private bool TryUpdateMaxPlayerPointsTrackerFromTeam(TeamDefinition teamOrdinal)
    {
        IEnumerable<Player> participatingPlayers = GetTeam(teamOrdinal).GetParticipatingPlayers();
        Player? maxTeamPointsPlayer = participatingPlayers.MaxBy(ip => ip.EventAggregate.Points);

        return maxTeamPointsPlayer != null
            && MaxPlayerPointsTracker.TryUpdateMaxPoints(maxTeamPointsPlayer.EventAggregate.Points, maxTeamPointsPlayer.Id);
    }

    #region Match Entity Availability Methods
    public bool IsCharacterAvailable(ulong characterId)
    {
        foreach (Team team in _ordinalTeamMap.Values)
        {
            if (team.ContainsPlayer(characterId))
            {
                return false;
            }
        }

        return true;
    }

    public bool IsOutfitOwnedByTeam(string alias, [NotNullWhen(true)] out Team? owningTeam)
    {
        foreach (Team team in _ordinalTeamMap.Values)
        {
            if (!team.ContainsOutfit(alias))
                continue;

            owningTeam = team;
            return true;
        }

        owningTeam = null;
        return false;
    }

    public bool IsConstructedTeamFactionAvailable(int constructedTeamId, FactionDefinition factionId)
    {
        foreach (Team team in _ordinalTeamMap.Values)
        {
            if (team.ContainsConstructedTeamFaction(constructedTeamId, factionId))
            {
                return false;
            }
        }

        return true;
    }

    public bool IsConstructedTeamFactionAvailable
    (
        int constructedTeamId,
        FactionDefinition factionId,
        [NotNullWhen(false)] out Team? owningTeam
    )
    {
        foreach (Team team in _ordinalTeamMap.Values)
        {
            if (!team.ContainsConstructedTeamFaction(constructedTeamId, factionId))
                continue;

            owningTeam = team;
            return false;
        }

        owningTeam = null;
        return true;
    }

    public bool IsConstructedTeamAnyFactionAvailable(int constructedTeamId)
    {
        foreach (FactionDefinition factionId in Enum.GetValues<FactionDefinition>())
        {
            if (IsConstructedTeamFactionAvailable(constructedTeamId, factionId))
            {
                return true;
            }
        }

        return false;
    }

    #endregion Match Entity Availability Methods

    public Player? GetPlayerFromId(ulong characterId)
    {
        TeamDefinition? teamOrdinal = GetTeamOrdinalFromPlayerId(characterId);
        if (teamOrdinal == null)
            return null;

        GetTeam(teamOrdinal.Value).TryGetPlayerFromId(characterId, out Player? player);

        return player;
    }

    public TeamDefinition? GetTeamOrdinalFromPlayerId(ulong characterId)
        => _playerTeamMap.TryGetValue(characterId, out TeamDefinition teamOrdinal)
            ? teamOrdinal
            : null;

    public bool DoPlayersShareTeam(Player? firstPlayer, Player? secondPlayer)
    {
        if (firstPlayer is null || secondPlayer is null)
            return false;

        return firstPlayer.TeamOrdinal == secondPlayer.TeamOrdinal;
    }

    private int TeamOutfitCount(TeamDefinition teamOrdinal)
        => GetTeam(teamOrdinal).Outfits.Count;

    private int TeamConstructedTeamCount(TeamDefinition teamOrdinal)
        => GetTeam(teamOrdinal).ConstructedTeamsMatchInfo.Count;

    #region Team/Player Stats Handling

    public async Task UpdatePlayerStats(ulong characterId, ScrimEventAggregate updates)
    {
        Player? player = GetPlayerFromId(characterId);
        if (player is null)
            return;

        player.AddStatsUpdate(updates);

        if (!player.IsBenched)
            player.IsActive = true;

        bool maxPointsChanged = MaxPlayerPointsTracker.TryUpdateMaxPoints(player.EventAggregate.Points, player.Id);

        OverlayMessageData overlayMessageData = new()
        {
            RedrawPointGraph = maxPointsChanged,
            MatchMaxPlayerPoints = MaxPlayerPointsTracker.MaxPoints
        };

        await SetPlayerParticipatingStatus(characterId, true);

        TeamDefinition? teamOrdinal = GetTeamOrdinalFromPlayerId(characterId);
        if (teamOrdinal is null)
            return;

        Team team = GetTeam(teamOrdinal.Value);
        team.AddStatsUpdate(updates);

        SendPlayerStatUpdateMessage(player, overlayMessageData);
        SendTeamStatUpdateMessage(team, overlayMessageData);
    }

    public void UpdateTeamStats(TeamDefinition teamOrdinal, ScrimEventAggregate updates)
    {
        Team team = GetTeam(teamOrdinal);

        team.AddStatsUpdate(updates);

        SendTeamStatUpdateMessage(team);
    }

    public int GetCurrentMatchRoundBaseControlsCount()
    {
        int totalControls = 0;

        foreach (TeamDefinition teamOrdinal in _ordinalTeamMap.Keys)
        {
            totalControls += GetCurrentMatchRoundTeamBaseControlsCount(teamOrdinal);
        }

        return totalControls;
    }

    public int GetCurrentMatchRoundTeamBaseControlsCount(TeamDefinition teamOrdinal)
    {
        int currentRound = _matchDataService.CurrentMatchRound;

        Team team = GetTeam(teamOrdinal);

        int roundControls = team.EventAggregateTracker.RoundStats.BaseControlVictories;

        if (team.EventAggregateTracker.TryGetTargetRoundStats(currentRound, out ScrimEventAggregate savedRoundStats))
        {
            roundControls += savedRoundStats.BaseControlVictories;
        }

        return roundControls;
    }

    public int GetCurrentMatchRoundTeamWeightedCapturesCount(TeamDefinition teamOrdinal)
    {
        int currentRound = _matchDataService.CurrentMatchRound;

        Team team = GetTeam(teamOrdinal);
        int roundControls = team.EventAggregateTracker.RoundStats.WeightedCapturesCount;

        if (team.EventAggregateTracker.TryGetTargetRoundStats(currentRound, out ScrimEventAggregate savedRoundStats))
        {
            roundControls += savedRoundStats.WeightedCapturesCount;
        }

        return roundControls;
    }

    #endregion Team/Player Stats Handling

    #region Match Results/Scores
    public async Task SaveRoundEndScoresAsync(int round, CancellationToken ct = default)
    {
        foreach (TeamDefinition teamOrdinal in _ordinalTeamMap.Keys.ToList())
        {
            SaveTeamRoundEndScores(teamOrdinal, round);

            await SaveTeamMatchResultsToDbAsync(teamOrdinal, ct);
        }
    }

    public void SaveTeamRoundEndScores(TeamDefinition teamOrdinal, int round)
    {
        Team team = GetTeam(teamOrdinal);
        team.EventAggregateTracker.SaveRoundToHistory(round);

        IEnumerable<Player> players = team.GetParticipatingPlayers();

        foreach (Player player in players)
        {
            player.EventAggregateTracker.SaveRoundToHistory(round);
        }
    }


    private async Task TryUpdateAllTeamMatchResultsInDbAsync(CancellationToken ct)
    {
        int currentMatchRound = _matchDataService.CurrentMatchRound;

        if (currentMatchRound <= 0)
        {
            return;
        }

        List<Task> TaskList = new();

        foreach (TeamDefinition teamOrdinal in _ordinalTeamMap.Keys)
        {
            Task<bool> teamTask = TryUpdateTeamMatchResultsInDbAsync(teamOrdinal, ct);
            TaskList.Add(teamTask);
        }

        await Task.WhenAll(TaskList);
    }

    // Update the ScrimMatchTeamResults row in the database if it exists, but don't create one if it doesn't.
    // Returns false if the result entry didn't exist or an error was encountered
    private async Task<bool> TryUpdateTeamMatchResultsInDbAsync(TeamDefinition teamOrdinal, CancellationToken ct)
    {
        int currentMatchRound = _matchDataService.CurrentMatchRound;

        if (currentMatchRound <= 0)
        {
            return false;
        }

        string currentScrimMatchId = _matchDataService.CurrentMatchId;

        try
        {
            await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

            ScrimMatchTeamResult? storeResultEntity = await dbContext.ScrimMatchTeamResults
                .FirstOrDefaultAsync
                (
                    result => result.ScrimMatchId == currentScrimMatchId && result.TeamOrdinal == teamOrdinal,
                    ct
                );

            if (storeResultEntity == null)
            {
                return false;
            }

            await SaveTeamMatchResultsToDbAsync(teamOrdinal, ct);

            _logger.LogInformation("Saved Team {TeamOrdinal} team match results to database", teamOrdinal);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update team match results in DB");

            return false;
        }
    }

    public async Task SaveTeamMatchResultsToDbAsync(TeamDefinition teamOrdinal, CancellationToken ct)
    {
        string currentScrimMatchId = _matchDataService.CurrentMatchId;

        ScrimEventAggregate resultsAggregate = new ScrimEventAggregate().Add(GetTeam(teamOrdinal).EventAggregate);

        ScrimMatchTeamResult resultsEntity = new()
        {
            ScrimMatchId = currentScrimMatchId,
            TeamOrdinal = teamOrdinal,
            Points = resultsAggregate.Points,
            NetScore = resultsAggregate.NetScore,
            Kills = resultsAggregate.Kills,
            Deaths = resultsAggregate.Deaths,
            Headshots = resultsAggregate.Headshots,
            HeadshotDeaths = resultsAggregate.HeadshotDeaths,
            Suicides = resultsAggregate.Suicides,
            Teamkills = resultsAggregate.Teamkills,
            TeamkillDeaths = resultsAggregate.TeamkillDeaths,
            RevivesGiven = resultsAggregate.RevivesGiven,
            RevivesTaken = resultsAggregate.RevivesTaken,
            DamageAssists = resultsAggregate.DamageAssists,
            UtilityAssists = resultsAggregate.UtilityAssists,
            DamageAssistedDeaths = resultsAggregate.DamageAssistedDeaths,
            UtilityAssistedDeaths = resultsAggregate.UtilityAssistedDeaths,
            ObjectiveCaptureTicks = resultsAggregate.ObjectiveCaptureTicks,
            ObjectiveDefenseTicks = resultsAggregate.ObjectiveDefenseTicks,
            BaseDefenses = resultsAggregate.BaseDefenses,
            BaseCaptures = resultsAggregate.BaseCaptures
        };

        try
        {
            await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

            ScrimMatchTeamResult? storeResultEntity = await dbContext.ScrimMatchTeamResults.FirstOrDefaultAsync(result => result.ScrimMatchId == currentScrimMatchId && result.TeamOrdinal == teamOrdinal);

            if (storeResultEntity == null)
            {
                dbContext.ScrimMatchTeamResults.Add(resultsEntity);
            }
            else
            {
                storeResultEntity = resultsEntity;
                dbContext.ScrimMatchTeamResults.Update(storeResultEntity);
            }

            // Team Results Point Adjustments
            List<PointAdjustment> updateAdjustments = resultsAggregate.PointAdjustments.ToList();

            List<ScrimMatchTeamPointAdjustment> storeAdjustmentEntities = await dbContext.ScrimMatchTeamPointAdjustments
                .Where(adj => adj.ScrimMatchId == currentScrimMatchId && adj.TeamOrdinal == teamOrdinal)
                .ToListAsync(ct);

            List<PointAdjustment> allAdjustments = new();

            allAdjustments.AddRange(updateAdjustments);
            allAdjustments.AddRange(storeAdjustmentEntities
                .Select(ConvertFromDbModel)
                .Where(e => allAdjustments.All(a => a.Timestamp != e.Timestamp))
                .ToList());

            List<ScrimMatchTeamPointAdjustment> createdAdjustments = new();

            foreach (PointAdjustment adjustment in allAdjustments)
            {
                ScrimMatchTeamPointAdjustment? storeEntity = storeAdjustmentEntities.FirstOrDefault(e => e.Timestamp == adjustment.Timestamp);
                PointAdjustment? updateAdjustment = updateAdjustments.FirstOrDefault(a => a.Timestamp == adjustment.Timestamp);

                if (storeEntity == null)
                {
                    ScrimMatchTeamPointAdjustment updateEntity = BuildScrimMatchTeamPointAdjustment(currentScrimMatchId, teamOrdinal, updateAdjustment);
                    createdAdjustments.Add(updateEntity);
                }
                else if (updateAdjustment == null)
                {
                    dbContext.ScrimMatchTeamPointAdjustments.Remove(storeEntity);
                }
                else
                {
                    ScrimMatchTeamPointAdjustment updateEntity = BuildScrimMatchTeamPointAdjustment(currentScrimMatchId, teamOrdinal, updateAdjustment);
                    storeEntity = updateEntity;
                    dbContext.ScrimMatchTeamPointAdjustments.Update(storeEntity);
                }
            }

            if (createdAdjustments.Any())
            {
                dbContext.ScrimMatchTeamPointAdjustments.AddRange(createdAdjustments);
            }

            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.ToString());
        }
    }
    #endregion Match Results/Scores

    #region Point Adjustments
    private PointAdjustment ConvertFromDbModel(ScrimMatchTeamPointAdjustment adjustment)
    {
        return new PointAdjustment
        {
            Timestamp = adjustment.Timestamp,
            Points = adjustment.Points,
            Rationale = adjustment.Rationale
        };
    }

    private ScrimMatchTeamPointAdjustment BuildScrimMatchTeamPointAdjustment(string scrimMatchId, TeamDefinition teamOrdinal, PointAdjustment adjustment)
    {
        return new ScrimMatchTeamPointAdjustment
        {
            ScrimMatchId = scrimMatchId,
            TeamOrdinal = teamOrdinal,
            Timestamp = adjustment.Timestamp,
            Points = adjustment.Points,
            AdjustmentType = adjustment.AdjustmentType,
            Rationale = adjustment.Rationale
        };
    }

    public async Task AdjustTeamPointsAsync
    (
        TeamDefinition teamOrdinal,
        PointAdjustment adjustment,
        CancellationToken ct = default
    )
    {
        ScrimEventAggregate statUpdate = new();

        statUpdate.AddPointAdjustment(adjustment);

        Team team = GetTeam(teamOrdinal);

        team.AddStatsUpdate(statUpdate);

        if (_matchDataService.CurrentMatchRound > 0)
        {
            await SaveTeamMatchResultsToDbAsync(teamOrdinal, ct);
        }

        SendTeamStatUpdateMessage(team);
    }

    public async Task RemoveTeamPointAdjustmentAsync
    (
        TeamDefinition teamOrdinal,
        PointAdjustment adjustment,
        CancellationToken ct = default
    )
    {
        ScrimEventAggregate statUpdate = new();

        statUpdate.AddPointAdjustment(adjustment);

        Team team = GetTeam(teamOrdinal);

        team.SubtractStatsUpdate(statUpdate);

        if (_matchDataService.CurrentMatchRound > 0)
        {
            await SaveTeamMatchResultsToDbAsync(teamOrdinal, ct);
        }

        SendTeamStatUpdateMessage(team);
    }
    #endregion Point Adjustments

    #region Player Status Updates

    public void SetPlayerOnlineStatus(ulong characterId, bool isOnline)
    {
        Player? player = GetPlayerFromId(characterId);
        if (player is null)
            return;

        player.IsOnline = isOnline;
        SendPlayerStatUpdateMessage(player);
    }

    public async Task SetPlayerParticipatingStatus(ulong characterId, bool isParticipating)
    {
        Player? player = GetPlayerFromId(characterId);
        if (player is null)
            return;

        bool wasAlreadyParticipating = player.IsParticipating;

        player.IsParticipating = isParticipating;
        player.IsActive = (!player.IsBenched && isParticipating);

        GetTeam(player.TeamOrdinal).UpdateParticipatingPlayer(player);

        if (wasAlreadyParticipating == isParticipating)
        {
            return;
        }

        SendPlayerStatUpdateMessage(player);

        if (!isParticipating)
        {
            await _matchDataService.TryRemoveMatchParticipatingPlayerAsync(characterId);
        }
        else if (isParticipating)
        {
            await _matchDataService.SaveMatchParticipatingPlayerAsync(player);
        }
    }

    public void SetPlayerBenchedStatus(ulong characterId, bool isBenched)
    {
        Player? player = GetPlayerFromId(characterId);
        if (player is null)
            return;

        player.IsBenched = isBenched;
        player.IsActive = !isBenched && player.IsParticipating;
        SendPlayerStatUpdateMessage(player);
    }

    public void SetPlayerLoadoutId(ulong characterId, uint? loadoutId)
    {
        if (loadoutId is null or <= 0)
            return;

        Player? player = GetPlayerFromId(characterId);
        if (player is null)
            return;

        player.LoadoutId = loadoutId;
        SendPlayerStatUpdateMessage(player);
    }

    #endregion

    #region Messaging
    private void SendTeamPlayerAddedMessage(Player player, bool isLastOfOutfit = false)
    {
        TeamPlayerChangeMessage payload = new(player, TeamPlayerChangeType.Add, isLastOfOutfit);
        _messageService.BroadcastTeamPlayerChangeMessage(payload);
    }

    private void SendTeamPlayerRemovedMessage(Player player)
    {
        TeamPlayerChangeMessage payload = new(player, TeamPlayerChangeType.Remove);
        _messageService.BroadcastTeamPlayerChangeMessage(payload);
    }

    private void SendTeamOutfitAddedMessage(Outfit outfit)
    {
        TeamOutfitChangeMessage payload = new(outfit, TeamChangeType.Add);
        _messageService.BroadcastTeamOutfitChangeMessage(payload);
    }

    private void SendTeamOutfitRemovedMessage(Outfit outfit)
    {
        TeamOutfitChangeMessage payload = new(outfit, TeamChangeType.Remove);
        _messageService.BroadcastTeamOutfitChangeMessage(payload);
    }

    private void SendTeamConstructedTeamRemovedMessage(TeamDefinition teamOrdinal, ConstructedTeamMatchInfo teamMatchInfo)
    {
        TeamConstructedTeamChangeMessage payload = new(teamOrdinal, teamMatchInfo.ConstructedTeam, teamMatchInfo.ActiveFactionId, TeamChangeType.Remove);

        _messageService.BroadcastTeamConstructedTeamChangeMessage(payload);
    }

    private void SendPlayerStatUpdateMessage(Player player)
    {
        PlayerStatUpdateMessage payload = new(player);
        _messageService.BroadcastPlayerStatUpdateMessage(payload);
    }

    private void SendPlayerStatUpdateMessage(Player player, OverlayMessageData overlayMessageData)
    {
        PlayerStatUpdateMessage payload = new(player, overlayMessageData);
        _messageService.BroadcastPlayerStatUpdateMessage(payload);
    }

    private void SendTeamStatUpdateMessage(Team team)
    {
        TeamStatUpdateMessage payload = new(team);
        _messageService.BroadcastTeamStatUpdateMessage(payload);
    }

    private void SendTeamStatUpdateMessage(Team team, OverlayMessageData overlayMessageData)
    {
        TeamStatUpdateMessage payload = new(team, overlayMessageData);
        _messageService.BroadcastTeamStatUpdateMessage(payload);
    }

    #endregion
}
