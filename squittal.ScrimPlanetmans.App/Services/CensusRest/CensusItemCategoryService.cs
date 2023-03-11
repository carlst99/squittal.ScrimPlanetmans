using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DbgCensus.Rest;
using DbgCensus.Rest.Abstractions;
using DbgCensus.Rest.Abstractions.Queries;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusRest;
using squittal.ScrimPlanetmans.App.Models.CensusRest;

namespace squittal.ScrimPlanetmans.App.Services.CensusRest;

/// <inheritdoc cref="ICensusItemCategoryService" />
public class CensusItemCategoryService : BaseCensusService, ICensusItemCategoryService
{
    private const uint INFANTRY_WEAPONS_CATEGORY_ID = 102;
    private static readonly uint[] INFANTRY_WEAPON_CATEGORY_PARENTS = { 102, 100 };

    private readonly CensusQueryOptions _sanctuaryQueryOptions;
    private readonly IMemoryCache _cache;

    public CensusItemCategoryService
    (
        ILogger<CensusItemCategoryService> logger,
        IQueryService queryService,
        IOptionsMonitor<CensusQueryOptions> queryOptions,
        IMemoryCache cache
    ) : base(logger, queryService)
    {
        _sanctuaryQueryOptions = queryOptions.Get("sanctuary");
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CensusItemCategory>?> GetAllAsync(CancellationToken ct = default)
    {
        object cacheKey = typeof(CensusItemCategory);

        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<CensusItemCategory>? itemCategories) && itemCategories is not null)
            return itemCategories;

        IQueryBuilder query = QueryService.CreateQuery(_sanctuaryQueryOptions)
            .OnCollection("item_category")
            .WithLimit(1000);

        itemCategories = await GetListAsync<CensusItemCategory>(query, ct);

        if (itemCategories is not null)
        {
            itemCategories = CorrectCategoryInheritance(itemCategories);
            _cache.Set(cacheKey, itemCategories, GetItemCategoriesCacheOptions());
        }

        return itemCategories;
    }

    public async Task<IEnumerable<CensusItemCategory>?> GetByIdAsync
    (
        IEnumerable<uint> categoryIds,
        CancellationToken ct = default
    )
    {
        IReadOnlyList<CensusItemCategory>? categories = await GetAllAsync(ct);

        return categories?.Where(c => categoryIds.Contains(c.ItemCategoryId));
    }

    public async Task<IEnumerable<CensusItemCategory>?> GetAllWeaponCategoriesAsync(CancellationToken ct = default)
    {
        IReadOnlyList<CensusItemCategory>? categories = await GetAllAsync(ct);
        return categories?.Where(c => c.ParentCategoryIds?.Contains(INFANTRY_WEAPONS_CATEGORY_ID) is true);
    }

    private static IReadOnlyList<CensusItemCategory> CorrectCategoryInheritance(IReadOnlyList<CensusItemCategory> categories)
    {
        CensusItemCategory[] corrected = new CensusItemCategory[categories.Count];

        for (int i = 0; i < categories.Count; i++)
        {
            CensusItemCategory category = categories[i];

            if (category.ItemCategoryId is 157 or 220 or 223) // Hybrid Rifles, Amphibious Rifles, Amphibious Sidearms
                category = category with { ParentCategoryIds = INFANTRY_WEAPON_CATEGORY_PARENTS };

            corrected[i] = category;
        }

        return corrected;
    }

    private static MemoryCacheEntryOptions GetItemCategoriesCacheOptions()
        => new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
            Priority = CacheItemPriority.Low
        };
}
