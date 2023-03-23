using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DbgCensus.Core.Objects;
using squittal.ScrimPlanetmans.App.Models.CensusRest;

namespace squittal.ScrimPlanetmans.App.Abstractions.Services.CensusRest;

public interface ICensusWorldService
{
    Task<IReadOnlyList<CensusWorld>?> GetAllAsync(CancellationToken ct = default);
    Task<CensusWorld?> GetByIdAsync(WorldDefinition worldId, CancellationToken ct = default);
}
