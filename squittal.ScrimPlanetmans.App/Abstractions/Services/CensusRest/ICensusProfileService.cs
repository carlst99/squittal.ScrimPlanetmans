using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models.CensusRest;

namespace squittal.ScrimPlanetmans.App.Abstractions.Services.CensusRest;

public interface ICensusProfileService
{
    Task<IReadOnlyList<CensusProfile>?> GetAllAsync(CancellationToken ct = default);
}
