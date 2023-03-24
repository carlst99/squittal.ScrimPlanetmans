using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DbgCensus.Core.Objects;
using DbgCensus.Rest.Abstractions;
using DbgCensus.Rest.Abstractions.Queries;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusRest;
using squittal.ScrimPlanetmans.App.Models.CensusRest;

namespace squittal.ScrimPlanetmans.App.Services.CensusRest;

/// <inheritdoc cref="ICensusWorldService" />
public class CensusWorldService : BaseCensusService, ICensusWorldService
{
    private readonly IMemoryCache _cache;

    public CensusWorldService
    (
        ILogger<CensusWorldService> logger,
        IQueryService queryService,
        IMemoryCache cache
    ) : base(logger, queryService)
    {
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CensusWorld>?> GetAllAsync(CancellationToken ct = default)
    {
        object cacheKey = typeof(CensusWorld);
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<CensusWorld>? worlds) && worlds is not null)
            return worlds;

        // Note: `/world` is a special collection that doesn't play ball with all `c:show/hide` filters
        IQueryBuilder query = QueryService.CreateQuery()
            .OnCollection("world");

        worlds = await QueryService.GetAsync<IReadOnlyList<CensusWorld>>(query, ct);

        if (worlds is not null)
            _cache.Set(cacheKey, worlds, GetWorldCacheOptions());

        return worlds;
    }

    /// <inheritdoc />
    public async Task<CensusWorld?> GetByIdAsync(WorldDefinition worldId, CancellationToken ct = default)
    {
        IReadOnlyList<CensusWorld>? worlds = await GetAllAsync(ct);
        return worlds?.FirstOrDefault(w => w.WorldId == worldId);
    }

    private static MemoryCacheEntryOptions GetWorldCacheOptions()
        => new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
            Priority = CacheItemPriority.Low
        };
}
