using System.Threading;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models.CensusRest;

namespace squittal.ScrimPlanetmans.App.Abstractions.Services.Planetside;

public interface ILoadoutService
{
    Task<bool> IsLoadoutOfProfileTypeAsync
    (
        uint loadoutId,
        CensusProfileType profileType,
        CancellationToken ct = default
    );
}
