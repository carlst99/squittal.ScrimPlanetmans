using System.Threading;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models.Planetside;

namespace squittal.ScrimPlanetmans.App.Services.Planetside.Interfaces;

public interface IOutfitService
{
    Task<Outfit?> GetByAliasAsync(string alias, CancellationToken ct = default);
}
