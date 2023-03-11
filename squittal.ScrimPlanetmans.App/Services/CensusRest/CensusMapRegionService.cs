using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DbgCensus.Rest.Abstractions;
using DbgCensus.Rest.Abstractions.Queries;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusRest;
using squittal.ScrimPlanetmans.App.Models.CensusRest;

namespace squittal.ScrimPlanetmans.App.Services.CensusRest;

/// <inheritdoc cref="ICensusMapRegionService" />
public class CensusMapRegionService : BaseCensusService, ICensusMapRegionService
{
    private readonly IMemoryCache _cache;

    public CensusMapRegionService
    (
        ILogger<CensusMapRegionService> logger,
        IQueryService queryService,
        IMemoryCache cache
    )
        : base(logger, queryService)
    {
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CensusMapRegion>?> GetAllAsync(CancellationToken ct = default)
    {
        object cacheKey = typeof(CensusMapRegion);

        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<CensusMapRegion>? mapRegions) && mapRegions is not null)
            return mapRegions;

        IQueryBuilder query = QueryService.CreateQuery()
            .OnCollection("map_region")
            .HasFields("facility_id")
            .ShowFields("map_region_id", "zone_id", "facility_id", "facility_name")
            .WithLimit(1000);

        mapRegions = await GetListAsync<CensusMapRegion>(query, ct);

        if (mapRegions is not null)
            _cache.Set(cacheKey, mapRegions, GetMapRegionCacheOptions());

        return mapRegions;
    }

    /// <inheritdoc />
    public async Task<CensusMapRegion?> GetByFacilityIdAsync(uint facilityId, CancellationToken ct = default)
    {
        IReadOnlyList<CensusMapRegion>? allRegions = await GetAllAsync(ct);
        return allRegions?.FirstOrDefault(r => r.FacilityId == facilityId);
    }

    private static MemoryCacheEntryOptions GetMapRegionCacheOptions()
        => new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
            Priority = CacheItemPriority.Low
        };
}
