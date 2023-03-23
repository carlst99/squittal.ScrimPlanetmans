using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DbgCensus.Rest.Abstractions;
using DbgCensus.Rest.Abstractions.Queries;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusRest;
using squittal.ScrimPlanetmans.App.Models.CensusRest;

namespace squittal.ScrimPlanetmans.App.Services.CensusRest;

/// <inheritdoc cref="ICensusZoneService" />
public class CensusZoneService : BaseCensusService, ICensusZoneService
{
    private readonly IMemoryCache _cache;

    public CensusZoneService
    (
        ILogger<CensusZoneService> logger,
        IQueryService queryService,
        IMemoryCache cache
    ) : base(logger, queryService)
    {
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CensusZone>?> GetAllAsync(CancellationToken ct = default)
    {
        object cacheKey = typeof(CensusZone);
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<CensusZone>? zones) && zones is not null)
            return zones;

        IQueryBuilder query = QueryService.CreateQuery()
            .OnCollection("zone")
            .ShowFields("zone_id", "name");

        zones = await QueryService.GetAsync<IReadOnlyList<CensusZone>>(query, ct);

        if (zones is not null)
            _cache.Set(cacheKey, zones, GetZoneCacheOptions());

        return zones;
    }

    private static MemoryCacheEntryOptions GetZoneCacheOptions()
        => new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
            Priority = CacheItemPriority.Low
        };
}
