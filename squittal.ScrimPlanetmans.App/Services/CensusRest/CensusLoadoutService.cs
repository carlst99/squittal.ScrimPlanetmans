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

/// <inheritdoc cref="ICensusLoadoutService" />
public class CensusLoadoutService : BaseCensusService, ICensusLoadoutService
{
    private readonly IMemoryCache _cache;

    public CensusLoadoutService
    (
        ILogger<CensusLoadoutService> logger,
        IQueryService queryService,
        IMemoryCache cache
    ) : base(logger, queryService)
    {
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CensusLoadout>?> GetAllAsync(CancellationToken ct = default)
    {
        object cacheKey = typeof(CensusLoadout);

        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<CensusLoadout>? loadouts) && loadouts is not null)
            return loadouts;

        IQueryBuilder query = QueryService.CreateQuery()
            .OnCollection("loadout")
            .ShowFields("loadout_id", "profile_id")
            .WithLimit(100);

        loadouts = await GetListAsync<CensusLoadout>(query, ct);

        if (loadouts is not null)
            _cache.Set(cacheKey, loadouts, GetLoadoutsCacheOptions());

        return loadouts;
    }

    private static MemoryCacheEntryOptions GetLoadoutsCacheOptions()
        => new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
            Priority = CacheItemPriority.Low
        };
}
