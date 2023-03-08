using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models.CensusRest;

namespace squittal.ScrimPlanetmans.App.Abstractions.Services.CensusRest;

public interface ICensusCharacterService
{
    Task<CensusCharacter?> GetByIdAsync(ulong characterId, CancellationToken ct = default);
    Task<IReadOnlyList<CensusCharacter>?> GetByIdAsync(IEnumerable<ulong> characterIds, CancellationToken ct = default);
    Task<CensusCharacter?> GetByNameAsync(string characterName, CancellationToken ct = default);
    Task<bool?> GetOnlineStatusAsync(ulong characterId, CancellationToken ct = default);

    Task<IReadOnlyList<CensusCharactersOnlineStatus>?> GetOnlineStatusAsync
    (
        IEnumerable<ulong> characterIds,
        CancellationToken ct = default
    );
}
