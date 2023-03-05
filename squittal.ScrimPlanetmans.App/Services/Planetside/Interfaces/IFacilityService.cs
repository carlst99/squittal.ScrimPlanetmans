using System.Collections.Generic;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models.Planetside;
using squittal.ScrimPlanetmans.App.Services.Interfaces;

namespace squittal.ScrimPlanetmans.App.Services.Planetside.Interfaces;

public interface IFacilityService : ILocallyBackedCensusStore
{
    Task<MapRegion> GetMapRegionAsync(int mapRegionId);
    Task<MapRegion> GetMapRegionFromFacilityIdAsync(int facilityId);
    Task<MapRegion> GetMapRegionFromFacilityNameAsync(string facilityName);

    Task<MapRegion> GetMapRegionsByFacilityTypeAsync(int facilityTypeId);

    Task SetUpScrimmableMapRegionsAsync();
    Task<MapRegion> GetScrimmableMapRegionFromFacilityIdAsync(int facilityId);
    Task<IEnumerable<MapRegion>> GetScrimmableMapRegionsAsync();
}
