using System.Collections.Generic;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Interfaces;

public interface IScrimTeamsManager
{
    MaxPlayerPointsTracker MaxPlayerPointsTracker { get; }

    Team GetTeam(TeamDefinition teamOrdinal);
    string GetTeamAliasDisplay(TeamDefinition teamOrdinal);
    Player? GetPlayerFromId(string characterId);
    IEnumerable<string> GetAllPlayerIds();
    bool UpdateTeamAlias(TeamDefinition teamOrdinal, string alias, bool isCustom = false);

    Task<bool> AddCharacterToTeam(TeamDefinition teamOrdinal, string characterId);
    Task<bool> AddOutfitAliasToTeam(TeamDefinition teamOrdinal, string aliasLower, string alias);

    Task<bool> RefreshOutfitPlayers(string aliasLower);

    bool RemoveOutfitFromTeam(string aliasLower);
    bool RemoveCharacterFromTeam(string characterId);

    void ClearAllTeams();
    void ClearTeam(TeamDefinition teamOrdinal);

    bool IsCharacterAvailable(string characterId, out Team owningTeam);
    bool IsCharacterAvailable(string characterId);

    bool IsOutfitOwnedByTeam(string alias, out Team? owningTeam);

    TeamDefinition? GetTeamOrdinalFromPlayerId(string characterId);
    bool DoPlayersShareTeam(string firstId, string secondId, out TeamDefinition? firstOrdinal, out TeamDefinition? secondOrdinal);

    bool DoPlayersShareTeam(Player? firstPlayer, Player? secondPlayer);
    Task<bool> TryAddFreeTextInputCharacterToTeamAsync(TeamDefinition teamOrdinal, string inputString);

    Task UpdatePlayerStats(string characterId, ScrimEventAggregate updates);
    void SetPlayerOnlineStatus(string characterId, bool isOnline);
    void SetPlayerLoadoutId(string characterId, int? loadoutId);
    Task SetPlayerParticipatingStatus(string characterId, bool isParticipating);
    void SetPlayerBenchedStatus(string characterId, bool isBenched);

    Task SaveRoundEndScores(int round);
    Task RollBackAllTeamStats(int currentRound);

    int? GetNextWorldId(int previousWorldId);
    Team? GetFirstTeamWithFactionId(int factionId);
    void UpdateTeamStats(TeamDefinition teamOrdinal, ScrimEventAggregate updates);

    Task AdjustTeamPoints(TeamDefinition teamOrdinal, PointAdjustment adjustment);
    Task RemoveTeamPointAdjustment(TeamDefinition teamOrdinal, PointAdjustment adjustment);

    Task<bool> RemoveOutfitFromTeamAndDb(string aliasLower);
    Task<bool> RemoveCharacterFromTeamAndDb(string characterId);
    int GetTeamScoreDisplay(TeamDefinition teamOrdinal);

    Task<bool> UpdatePlayerTemporaryAliasAsync(string playerId, string newAlias);
    Task ClearPlayerDisplayNameAsync(string playerId);

    Task<bool> AddConstructedTeamFactionMembersToTeam(TeamDefinition teamOrdinal, int constructedTeamId, int factionId);
    IEnumerable<Player> GetTeamOutfitPlayers(TeamDefinition teamOrdinal, string outfitAliasLower);
    IEnumerable<Player> GetTeamNonOutfitPlayers(TeamDefinition teamOrdinal);
    IEnumerable<Player> GetTeamConstructedTeamFactionPlayers(TeamDefinition teamOrdinal, int constructedTeamId, int factionId);
    Task<bool> RemoveConstructedTeamFactionFromTeamAndDb(int constructedTeamId, int factionId);
    bool RemoveConstructedTeamFactionFromTeam(int constructedTeamId, int factionId);
    bool IsConstructedTeamFactionAvailable(int constructedTeamId, int factionId, out Team? owningTeam);
    bool IsConstructedTeamFactionAvailable(int constructedTeamId, int factionId);
    Team? GetTeamFromConstructedTeamFaction(int constructedTeamId, int factionId);
    bool IsConstructedTeamAnyFactionAvailable(int constructedTeamId);
    void ResetAllTeamsMatchData();
    Task LockTeamPlayers(TeamDefinition teamOrdinal);
    void UnlockTeamPlayers(TeamDefinition teamOrdinal);
    bool GetTeamLockStatus(TeamDefinition teamOrdinal);
    void UnlockAllTeamPlayers();
    int GetCurrentMatchRoundTeamBaseControlsCount(TeamDefinition teamOrdinal);
    int GetCurrentMatchRoundBaseControlsCount();
    int GetCurrentMatchRoundWeightedCapturesCount();
    int GetCurrentMatchRoundTeamWeightedCapturesCount(TeamDefinition teamOrdinal);
    void UpdateAllTeamsMatchSeriesResults(int seriesMatchNumber);
    void UpdateAllTeamsMatchSeriesResults(TeamDefinition teamOrdinal, int seriesMatchNumber, ScrimSeriesMatchResultType matchResultType);
    List<ScrimSeriesMatchResult> GetTeamsScrimSeriesMatchResults(TeamDefinition teamOrdinal);
}
