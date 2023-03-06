using System.Collections.Generic;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models.Planetside;
using squittal.ScrimPlanetmans.App.Services.Interfaces;

namespace squittal.ScrimPlanetmans.App.Services.Planetside.Interfaces;

public interface IProfileService : ILocallyBackedCensusStore
{
    Task<IEnumerable<Profile>> GetAllProfilesAsync();
    Task<Profile?> GetProfileFromLoadoutIdAsync(int loadoutId);
}
