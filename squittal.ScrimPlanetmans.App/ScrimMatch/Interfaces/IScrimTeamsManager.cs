﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DbgCensus.Core.Objects;
using squittal.ScrimPlanetmans.App.Models;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Interfaces;

public interface IScrimTeamsManager
{
    MaxPlayerPointsTracker MaxPlayerPointsTracker { get; }
    Team GetTeam(TeamDefinition teamOrdinal);
    string GetTeamAliasDisplay(TeamDefinition teamOrdinal);
    Player? GetPlayerFromId(ulong characterId);
    IEnumerable<ulong> GetAllPlayerIds();
    bool UpdateTeamAlias(TeamDefinition teamOrdinal, string alias, bool isCustom = false);
    Task<bool> AddOutfitAliasToTeam(TeamDefinition teamOrdinal, string aliasLower);
    Task<bool> RefreshOutfitPlayers(string aliasLower);
    void ClearAllTeams();
    bool IsOutfitOwnedByTeam(string alias, out Team? owningTeam);
    bool DoPlayersShareTeam(Player? firstPlayer, Player? secondPlayer);
    Task<bool> TryAddFreeTextInputCharacterToTeamAsync(TeamDefinition teamOrdinal, string inputString);
    Task UpdatePlayerStats(ulong characterId, ScrimEventAggregate updates);
    void SetPlayerOnlineStatus(ulong characterId, bool isOnline);
    void SetPlayerLoadoutId(ulong characterId, uint? loadoutId);
    void SetPlayerBenchedStatus(ulong characterId, bool isBenched);
    Task SaveRoundEndScoresAsync(int round, CancellationToken ct = default);
    Task RollBackAllTeamStatsAsync(int currentRound, CancellationToken ct = default);
    WorldDefinition? GetNextWorldId(WorldDefinition previousWorldId);
    Team? GetFirstTeamWithFactionId(FactionDefinition factionId);
    void UpdateTeamStats(TeamDefinition teamOrdinal, ScrimEventAggregate updates);
    Task AdjustTeamPointsAsync(TeamDefinition teamOrdinal, PointAdjustment adjustment, CancellationToken ct = default);
    Task RemoveTeamPointAdjustmentAsync(TeamDefinition teamOrdinal, PointAdjustment adjustment, CancellationToken ct = default);
    Task<bool> RemoveOutfitFromTeamAndDbAsync(string aliasLower, CancellationToken ct = default);
    Task<bool> RemoveCharacterFromTeamAndDbAsync(ulong characterId, CancellationToken ct = default);
    int GetTeamScoreDisplay(TeamDefinition teamOrdinal);
    Task<bool> UpdatePlayerTemporaryAliasAsync(ulong playerId, string newAlias);
    Task ClearPlayerDisplayNameAsync(ulong playerId);
    Task<bool> AddConstructedTeamFactionMembersToTeam(TeamDefinition teamOrdinal, int constructedTeamId, FactionDefinition factionId);
    IEnumerable<Player> GetTeamOutfitPlayers(TeamDefinition teamOrdinal, string outfitAliasLower);
    IEnumerable<Player> GetTeamNonOutfitPlayers(TeamDefinition teamOrdinal);

    IEnumerable<Player> GetTeamConstructedTeamFactionPlayers
    (
        TeamDefinition teamOrdinal,
        int constructedTeamId,
        FactionDefinition factionId
    );

    Task<bool> RemoveConstructedTeamFactionFromTeamAndDbAsync
    (
        int constructedTeamId,
        FactionDefinition factionId,
        CancellationToken ct = default
    );
    bool IsConstructedTeamFactionAvailable(int constructedTeamId, FactionDefinition factionId);
    bool IsConstructedTeamAnyFactionAvailable(int constructedTeamId);
    void ResetAllTeamsMatchData();
    Task LockTeamPlayers(TeamDefinition teamOrdinal);
    void UnlockTeamPlayers(TeamDefinition teamOrdinal);
    bool GetTeamLockStatus(TeamDefinition teamOrdinal);
    int GetCurrentMatchRoundBaseControlsCount();
    int GetCurrentMatchRoundTeamWeightedCapturesCount(TeamDefinition teamOrdinal);
    void UpdateAllTeamsMatchSeriesResults(int seriesMatchNumber);
    List<ScrimSeriesMatchResult> GetTeamsScrimSeriesMatchResults(TeamDefinition teamOrdinal);
}
