using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models.Planetside;
using squittal.ScrimPlanetmans.App.Services.Interfaces;

namespace squittal.ScrimPlanetmans.App.Services.Planetside.Interfaces;

public interface IVehicleService : ILocallyBackedCensusStore
{
    Task<Vehicle?> GetVehicleInfoAsync(int vehicleId);

    Vehicle GetScrimVehicleInfo(int vehicleId);

    Task SetUpScrimmableVehicleInfosList();
}
