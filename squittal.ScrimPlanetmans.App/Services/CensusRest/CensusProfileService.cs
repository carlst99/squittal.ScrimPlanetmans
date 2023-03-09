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

/// <inheritdoc cref="ICensusProfileService" />
public class CensusProfileService : BaseCensusService, ICensusProfileService
{
    private readonly IMemoryCache _cache;

    public CensusProfileService
    (
        ILogger<CensusProfileService> logger,
        IQueryService queryService,
        IMemoryCache cache
    ) : base(logger, queryService)
    {
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CensusProfile>?> GetAllAsync(CancellationToken ct = default)
    {
        object cacheKey = typeof(CensusProfile);

        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<CensusProfile>? Profiles) && Profiles is not null)
            return Profiles;

        IQueryBuilder query = QueryService.CreateQuery()
            .OnCollection("profile")
            .ShowFields("profile_id", "profile_type_id")
            .WithLimit(100);

        Profiles = await GetListAsync<CensusProfile>(query, ct);

        if (Profiles is not null)
            _cache.Set(cacheKey, Profiles, GetProfilesCacheOptions());

        return Profiles;
    }

    private static MemoryCacheEntryOptions GetProfilesCacheOptions()
        => new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
            Priority = CacheItemPriority.Low
        };
}
