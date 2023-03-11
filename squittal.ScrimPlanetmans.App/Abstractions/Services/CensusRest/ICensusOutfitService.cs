using System.Threading;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models.CensusRest;

namespace squittal.ScrimPlanetmans.App.Abstractions.Services.CensusRest;

public interface ICensusOutfitService
{
    Task<CensusOutfit?> GetByAliasAsync(string outfitAlias, CancellationToken ct = default);
}
