using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models.CensusRest;

namespace squittal.ScrimPlanetmans.App.Abstractions.Services.CensusRest;

public interface ICensusMapRegionService
{
    Task<IReadOnlyList<CensusMapRegion>?> GetAllAsync(CancellationToken ct = default);
    Task<CensusMapRegion?> GetByFacilityIdAsync(uint facilityId, CancellationToken ct = default);
}
