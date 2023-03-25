using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using DbgCensus.Core.Objects;
using squittal.ScrimPlanetmans.App.Data.Models;
using squittal.ScrimPlanetmans.App.Models;
using squittal.ScrimPlanetmans.App.Models.Planetside;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch;

public class Team
{
    private readonly ConcurrentDictionary<string, ConstructedTeamMatchInfo> constructedTeamsMap = new();
    private readonly ConcurrentDictionary<ulong, Player> _playersMap = new();
    private readonly ConcurrentDictionary<string, Outfit> _outfitsMap = new();
    private readonly ConcurrentDictionary<ulong, Player> _participatingPlayersMap = new();

    private bool _hasCustomAlias;

    public string Alias { get; set; }
    public string NameInternal { get; }
    public TeamDefinition TeamOrdinal { get; }
    public FactionDefinition? FactionId { get; set; }

    public bool IsLocked { get; set; }

    public ScrimEventAggregate EventAggregate => EventAggregateTracker.TotalStats;

    public ScrimEventAggregateRoundTracker EventAggregateTracker { get; set; } = new();

    public List<Player> Players { get; } = new();

    public List<Player> ParticipatingPlayers { get; set; } = new();

    public List<Outfit> Outfits { get; } = new();

    public List<ConstructedTeamMatchInfo> ConstructedTeamsMatchInfo { get; set; } = new();

    public List<ScrimSeriesMatchResult> ScrimSeriesMatchResults { get; } = new();

    public Team(string alias, string nameInternal, TeamDefinition teamOrdinal)
    {
        TrySetAlias(alias);
        NameInternal = nameInternal;
        TeamOrdinal = teamOrdinal;
    }

    [MemberNotNull(nameof(Alias))]
    public bool TrySetAlias(string alias, bool isCustomAlias = false)
    {
        // Don't overwrite a custom display alias unless the new one is also custom
        if (_hasCustomAlias && !isCustomAlias && Alias is not null)
            return false;

        Alias = alias;
        _hasCustomAlias = isCustomAlias;
        return true;
    }

    public void ResetAlias(string alias)
    {
        Alias = alias;
        _hasCustomAlias = false;
    }


    #region Team Players

    public bool ContainsPlayer(ulong characterId)
        => _playersMap.ContainsKey(characterId);

    public IEnumerable<ulong> GetAllPlayerIds()
        => _playersMap.Keys;

    public bool TryGetPlayerFromId(ulong characterId, [NotNullWhen(true)] out Player? player)
        => _playersMap.TryGetValue(characterId, out player);

    public bool TryAddPlayer(Player player)
    {
        if(!_playersMap.TryAdd(player.Id, player))
        {
            return false;
        }

        Players.Add(player);

        return true;
    }

    public bool TryRemovePlayer(ulong characterId)
    {
        if (!_playersMap.TryRemove(characterId, out Player? player))
        {
            return false;
        }

        Players.Remove(player);

        ParticipatingPlayers.Remove(player);

        _participatingPlayersMap.TryRemove(player.Id, out Player? removedPlayer);

        RemovePlayerObjectiveTicksFromTeamAggregate(player); // TODO: remove this when Objective Ticks are saved to DB

        return true;
    }

    private void RemovePlayerObjectiveTicksFromTeamAggregate(Player player)
    {
        var teamUpdates = new ScrimEventAggregateRoundTracker();

        var playerTracker = player.EventAggregateTracker;

        var playerMaxRound = playerTracker.HighestRound;
        var teamMaxRound = EventAggregateTracker.HighestRound;

        var maxRound = playerMaxRound >= teamMaxRound ? playerMaxRound : teamMaxRound;

        for (var round = 1; round <= maxRound; round++)
        {
            if (playerTracker.TryGetTargetRoundStats(round, out var roundStats))
            {
                var tempStats = new ScrimEventAggregate();

                tempStats.ObjectiveCaptureTicks += roundStats.ObjectiveCaptureTicks;
                tempStats.ObjectiveDefenseTicks += roundStats.ObjectiveDefenseTicks;

                teamUpdates.AddToCurrent(tempStats);

                teamUpdates.SaveRoundToHistory(round);
            }
        }

        teamUpdates.RoundStats.ObjectiveCaptureTicks += playerTracker.RoundStats.ObjectiveCaptureTicks;
        teamUpdates.RoundStats.ObjectiveDefenseTicks += playerTracker.RoundStats.ObjectiveDefenseTicks;

        EventAggregateTracker.SubtractFromHistory(teamUpdates);
    }

    public IEnumerable<Player> GetNonOutfitPlayers()
    {
        lock (Players)
        {
            return Players.Where(p => p.IsOutfitless && !p.IsFromConstructedTeam).ToList();
        }
    }

    public bool UpdateParticipatingPlayer(Player player)
    {
        var playerId = player.Id;

        if (player.IsParticipating)
        {
            return _participatingPlayersMap.TryAdd(playerId, player);
        }
        else
        {
            return _participatingPlayersMap.TryRemove(playerId, out Player removedPlayer);
        }
    }

    public IEnumerable<Player> GetParticipatingPlayers()
    {
        return _participatingPlayersMap.Values.ToList();
    }
    #endregion Team Players

    #region Team Outfits
    public bool ContainsOutfit(string alias)
    {
        return _outfitsMap.ContainsKey(alias.ToLower());
    }
    public bool TryAddOutfit(Outfit outfit)
    {
        if (!_outfitsMap.TryAdd(outfit.AliasLower, outfit))
        {
            return false;
        }

        Outfits.Add(outfit);
            
        return true;
    }

    public bool TryRemoveOutfit(string aliasLower)
    {
        if (!_outfitsMap.TryRemove(aliasLower, out var outfitOut))
        {
            return false;
        }

        Outfits.RemoveAll(o => o.AliasLower == aliasLower);

        return true;
    }

    public IEnumerable<Player> GetOutfitPlayers(string aliasLower)
    {
        lock (Players)
        {
            return Players.Where(p => p.OutfitAliasLower == aliasLower && !p.IsOutfitless && !p.IsFromConstructedTeam).ToList();
        }
    }
    #endregion Team Outfits

    #region Team Contstructed Teams
    public bool ContainsConstructedTeamFaction(int constructedTeamId, FactionDefinition factionId)
    {
        return constructedTeamsMap.ContainsKey(GetConstructedTeamFactionKey(constructedTeamId, factionId));
    }

    public bool TryAddConstructedTeamFaction(ConstructedTeamMatchInfo matchInfo)
    {
        ConstructedTeam? constructedTeam = matchInfo.ConstructedTeam;
        if (constructedTeam is null)
            return false;

        FactionDefinition factionId = matchInfo.ActiveFactionId;
        if (!constructedTeamsMap.TryAdd(GetConstructedTeamFactionKey(constructedTeam.Id, factionId), matchInfo))
            return false;

        ConstructedTeamsMatchInfo.Add(matchInfo);

        return true;
    }

    public bool TryRemoveConstructedTeamFaction(int constructedTeamId, FactionDefinition factionId)
    {
        if (!constructedTeamsMap.TryRemove(GetConstructedTeamFactionKey(constructedTeamId, factionId), out _))
        {
            return false;
        }

        ConstructedTeamsMatchInfo.RemoveAll(ctmi => ctmi.ConstructedTeam?.Id == constructedTeamId && ctmi.ActiveFactionId == factionId);

        return true;
    }

    public IEnumerable<Player> GetConstructedTeamFactionPlayers(int constructedTeamId, FactionDefinition factionId)
    {
        lock (Players)
        {
            return Players
                .Where(p => p.IsFromConstructedTeam && p.ConstructedTeamId == constructedTeamId && p.FactionId == factionId)
                .ToList();
        }
    }

    private string GetConstructedTeamFactionKey(int constructedTeamId, FactionDefinition factionId)
    {
        return $"{constructedTeamId}^{(int)factionId}";
    }
    #endregion Team Contstructed Teams

    #region Team Stats & Match Data
    public void ResetMatchData()
    {
        ClearEventAggregateHistory();

        ParticipatingPlayers.Clear();
        _participatingPlayersMap.Clear();
    }

    public void AddStatsUpdate(ScrimEventAggregate update)
    {
        EventAggregateTracker.AddToCurrent(update);
    }

    public void SubtractStatsUpdate(ScrimEventAggregate update)
    {
        EventAggregateTracker.SubtractFromCurrent(update);
    }

    public void ClearEventAggregateHistory()
    {
        EventAggregateTracker = new ScrimEventAggregateRoundTracker();
    }

    public void UpdateScrimSeriesMatchResults(int seriesMatchNumber, ScrimSeriesMatchResultType seriesMatchResultType)
    {
        var existingResult = GetScrimSeriesMatchResult(seriesMatchNumber);

        if (existingResult != null)
        {
            existingResult.ResultType = seriesMatchResultType;
        }
        else
        {
            ScrimSeriesMatchResults.Add(new ScrimSeriesMatchResult(seriesMatchNumber, seriesMatchResultType));
        }
    }

    public ScrimSeriesMatchResult GetScrimSeriesMatchResult(int seriesMatchNumber)
    {
        return ScrimSeriesMatchResults.Where(r => r.MatchNumber == seriesMatchNumber).FirstOrDefault();
    }

    public void ClearScrimSeriesMatchResults()
    {
        ScrimSeriesMatchResults.Clear();
    }
    #endregion Team Stats & Match Data

}
