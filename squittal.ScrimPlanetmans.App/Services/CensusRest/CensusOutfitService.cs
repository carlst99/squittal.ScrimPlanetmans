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

/// <inheritdoc cref="ICensusOutfitService" />
public class CensusOutfitService : BaseCensusService, ICensusOutfitService
{
    private readonly IMemoryCache _cache;

    public CensusOutfitService
    (
        ILogger<CensusOutfitService> logger,
        IQueryService queryService,
        IMemoryCache cache
    ) : base(logger, queryService)
    {
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<CensusOutfit?> GetByAliasAsync(string outfitAlias, CancellationToken ct = default)
    {
        object cacheKey = (typeof(CensusOutfit), outfitAlias);

        if (_cache.TryGetValue(cacheKey, out CensusOutfit? outfit) && outfit is not null)
            return outfit;

        IQueryBuilder query = QueryService.CreateQuery()
            .OnCollection("outfit")
            .Where("alias_lower", SearchModifier.Equals, outfitAlias.ToLower())
            .ShowFields("outfit_id", "name", "alias", "leader_character_id")
            .AddJoin("outift_member", j =>
            {
                j.IsList()
                    .ShowFields("character_id")
                    .InjectAt("members");
            });

        outfit = await GetAsync<CensusOutfit>(query, ct);

        if (outfit is not null)
            _cache.Set(cacheKey, outfit, GetOutfitCacheOptions());

        return outfit;
    }

    private static MemoryCacheEntryOptions GetOutfitCacheOptions()
        => new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
            Priority = CacheItemPriority.Low
        };
}
