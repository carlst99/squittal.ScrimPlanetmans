﻿using System.Collections.Generic;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models.Planetside;
using squittal.ScrimPlanetmans.App.Services.Interfaces;

namespace squittal.ScrimPlanetmans.App.Services.Planetside.Interfaces;

public interface IWorldService : ILocallyBackedCensusStore
{
    Task<IEnumerable<World>> GetAllWorldsAsync();
    Task<World?> GetWorldAsync(int worldId);
    Task SetUpWorldsMap();
}
