using System.Collections.Generic;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.Services.ScrimMatch.Interfaces;

public interface IDeathEventTypeService
{
    Task SeedDeathTypes();
    IEnumerable<DeathEventType> GetDeathEventTypes();
}
