﻿using squittal.ScrimPlanetmans.Shared.Models.Planetside;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace squittal.ScrimPlanetmans.Services.Planetside
{
    public interface IProfileService
    {
        Task<IEnumerable<Profile>> GetAllProfilesAsync();
        Task<IEnumerable<Loadout>> GetAllLoadoutsAsync();
        Task<Profile> GetProfileFromLoadoutIdAsync(int loadoutId);
        Task<Dictionary<int, Profile>> GetLoadoutMapping();
        Task RefreshStore();
    }
}
