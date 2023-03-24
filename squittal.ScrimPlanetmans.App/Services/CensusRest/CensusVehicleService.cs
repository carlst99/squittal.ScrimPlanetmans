using System;
using System.Threading;
using System.Threading.Tasks;
using DbgCensus.Rest.Abstractions;
using DbgCensus.Rest.Abstractions.Queries;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusRest;
using squittal.ScrimPlanetmans.App.Models.CensusRest;

namespace squittal.ScrimPlanetmans.App.Services.CensusRest;

public class CensusVehicleService : BaseCensusService, ICensusVehicleService
{
    private readonly IMemoryCache _cache;

    public CensusVehicleService
    (
        ILogger<CensusVehicleService> logger,
        IQueryService queryService,
        IMemoryCache cache
    ) : base(logger, queryService)
    {
        _cache = cache;
    }

    public async Task<CensusVehicle?> GetByIdAsync(uint vehicleId, CancellationToken ct = default)
    {
        object cacheKey = (typeof(CensusVehicle), vehicleId);
        if (_cache.TryGetValue(cacheKey, out CensusVehicle? vehicle) && vehicle is not null)
            return vehicle;

        IQueryBuilder query = QueryService.CreateQuery()
            .OnCollection("vehicle")
            .Where("vehicle_id", SearchModifier.Equals, vehicleId)
            .ShowFields("vehicle_id", "name")
            .WithLimit(500);

        vehicle = await QueryService.GetAsync<CensusVehicle>(query, ct);

        if (vehicle is not null)
            _cache.Set(cacheKey, vehicle, GetVehicleCacheOptions());

        return vehicle;
    }

    private static MemoryCacheEntryOptions GetVehicleCacheOptions()
        => new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
            Priority = CacheItemPriority.Low
        };
}
