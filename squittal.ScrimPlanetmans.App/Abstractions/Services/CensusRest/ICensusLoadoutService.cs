using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models.CensusRest;

namespace squittal.ScrimPlanetmans.App.Abstractions.Services.CensusRest;

public interface ICensusLoadoutService
{
    Task<IReadOnlyList<CensusLoadout>?> GetAllAsync(CancellationToken ct = default);
}
