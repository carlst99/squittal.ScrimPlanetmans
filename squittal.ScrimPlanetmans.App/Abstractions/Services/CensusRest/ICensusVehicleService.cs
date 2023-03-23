using System.Threading;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models.CensusRest;

namespace squittal.ScrimPlanetmans.App.Abstractions.Services.CensusRest;

public interface ICensusVehicleService
{
    Task<CensusVehicle?> GetByIdAsync(uint vehicleId, CancellationToken ct = default);
}
