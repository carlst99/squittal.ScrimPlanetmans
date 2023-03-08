using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Data.Models;
using squittal.ScrimPlanetmans.App.Models;
using squittal.ScrimPlanetmans.App.Models.CensusRest;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.Services.ScrimMatch.Interfaces;

public interface IConstructedTeamService
{
    Task<ConstructedTeam?> GetConstructedTeamAsync
    (
        int teamId,
        bool ignoreCollections = false,
        CancellationToken ct = default
    );

    Task<ConstructedTeamFormInfo?> GetConstructedTeamFormInfoAsync
    (
        int teamId,
        bool ignoreCollections = false,
        CancellationToken ct = default
    );

    Task<ConstructedTeam?> CreateConstructedTeamAsync
    (
        ConstructedTeam constructedTeam,
        CancellationToken ct = default
    );

    Task<CensusCharacter?> TryAddCharacterToConstructedTeamAsync
    (
        int teamId,
        string characterInput,
        string customAlias,
        CancellationToken ct = default
    );

    Task<bool> TryRemoveCharacterFromConstructedTeamAsync
    (
        int teamId,
        ulong characterId,
        CancellationToken ct = default
    );

    Task<bool> UpdateConstructedTeamInfo(ConstructedTeam teamUpdate);
    Task<int> GetConstructedTeamMemberCount(int teamId);

    Task<IEnumerable<ConstructedTeamMemberDetails>> GetConstructedTeamFactionMemberDetailsAsync
    (
        int teamId,
        int factionId,
        CancellationToken ct = default
    );

    Task<IEnumerable<Player>> GetConstructedTeamFactionPlayersAsync
    (
        int teamId,
        int factionId,
        CancellationToken ct = default
    );

    Task<bool> TryUpdateMemberAliasAsync
    (
        int teamId,
        ulong characterId,
        string oldAlias,
        string newAlias,
        CancellationToken ct = default
    );

    Task<IEnumerable<ConstructedTeam>> GetConstructedTeamsAsync
    (
        bool includeHiddenTeams = false,
        CancellationToken ct = default
    );
}
