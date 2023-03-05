using System.Collections.Generic;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models.Planetside;
using squittal.ScrimPlanetmans.App.Services.Interfaces;

namespace squittal.ScrimPlanetmans.App.Services.Planetside.Interfaces;

public interface IFactionService : ILocallyBackedCensusStore
{
    Task<IEnumerable<Faction>> GetAllFactionsAsync();
    string GetFactionAbbrevFromId(int factionId);
    Task<Faction> GetFactionAsync(int factionId);
}
