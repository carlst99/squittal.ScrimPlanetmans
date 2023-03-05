using System.Collections.Generic;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models.Planetside;
using squittal.ScrimPlanetmans.App.Services.Interfaces;

namespace squittal.ScrimPlanetmans.App.Services.Planetside.Interfaces;

public interface IZoneService : ILocallyBackedCensusStore
{
    Task<IEnumerable<Zone>> GetAllZones();
    Task<IEnumerable<Zone>> GetAllZonesAsync();
    Task<Zone> GetZoneAsync(int zoneId);
    Task SetupZonesMapAsync();
}
