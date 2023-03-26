using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.Abstractions.Services.ScrimMatch;

public interface IScrimPlayersService
{
    Task<Player?> GetByIdAsync(ulong characterId, CancellationToken ct = default);
    Task<IEnumerable<Player>?> GetByIdAsync(IEnumerable<ulong> characterIds, CancellationToken ct = default);
    Task<Player?> GetByNameAsync(string characterName, CancellationToken ct = default);
    Task<IEnumerable<Player>?> GetPlayersFromOutfitAliasAsync(string alias, CancellationToken ct = default);
}
