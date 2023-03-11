using System.Threading;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models.Planetside;

namespace squittal.ScrimPlanetmans.App.Abstractions.Services.Planetside;

public interface IOutfitService
{
    Task<Outfit?> GetByAliasAsync(string alias, CancellationToken ct = default);
}
