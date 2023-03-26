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

/// <inheritdoc cref="ICensusItemService" />
public class CensusItemService : BaseCensusService, ICensusItemService
{
    private readonly ICensusItemCategoryService _categoryService;
    private readonly IMemoryCache _cache;

    public CensusItemService
    (
        ILogger<CensusItemService> logger,
        IQueryService queryService,
        ICensusItemCategoryService categoryService,
        IMemoryCache cache
    ) : base(logger, queryService)
    {
        _categoryService = categoryService;
        _cache = cache;
    }

    public async Task<CensusItem?> GetByIdAsync(uint itemId, CancellationToken ct = default)
    {
        object cacheKey = GetItemCacheKey(itemId);

        if (_cache.TryGetValue(cacheKey, out CensusItem? item) && item is not null)
            return item;

        IQueryBuilder query = CreateDefaultItemQuery()
            .Where("item_id", SearchModifier.Equals, itemId);

        item = await GetAsync<CensusItem>(query, ct);

        if (item is not null)
            _cache.Set(cacheKey, item);

        return item;
    }

    public async Task<IReadOnlyList<CensusItem>> GetByIdAsync(IEnumerable<uint> itemIds, CancellationToken ct = default)
    {
        List<CensusItem> retrieved = new();
        List<uint> toRetrieve = new();

        foreach (uint itemId in itemIds)
        {
            object cacheKey = GetItemCacheKey(itemId);

            if (_cache.TryGetValue(cacheKey, out CensusItem? item) && item is not null)
                retrieved.Add(item);
            else
                toRetrieve.Add(itemId);
        }

        IQueryBuilder query = CreateDefaultItemQuery()
            .WhereAll("item_id", SearchModifier.Equals, toRetrieve);

        IReadOnlyList<CensusItem>? getItems = await GetListAsync<CensusItem>(query, ct);
        if (getItems is null)
            return retrieved;

        CacheItems(getItems);
        retrieved.AddRange(getItems);

        return retrieved;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CensusItem>?> GetAllWeaponsAsync(CancellationToken ct = default)
    {
        IEnumerable<CensusItemCategory>? weaponCategories = await _categoryService.GetAllWeaponCategoriesAsync(ct);
        if (weaponCategories is null)
            return null;

        List<CensusItem> weapons = new();
        foreach (CensusItemCategory category in weaponCategories)
        {
            IReadOnlyList<CensusItem>? categoryItems = await GetByCategoryAsync(category.ItemCategoryId, ct);
            if (categoryItems is not null)
                weapons.AddRange(categoryItems);
        }

        return weapons.Count is 0
            ? null
            : weapons;
    }

    public async Task<IReadOnlyList<CensusItem>?> GetByCategoryAsync
    (
        uint itemCategoryId,
        CancellationToken ct = default
    )
    {
        object cacheKey = GetCategoryBundleCacheKey(itemCategoryId);

        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<CensusItem>? cachedItems) && cachedItems is not null)
            return cachedItems;

        IQueryBuilder query = CreateDefaultItemQuery()
            .Where("item_category_id", SearchModifier.Equals, itemCategoryId);

        List<CensusItem> items = new();
        IAsyncEnumerable<IEnumerable<CensusItem>> pages = QueryService.GetPaginatedAsync<CensusItem>
        (
            query,
            1000,
            ct: ct
        );

        await foreach (IEnumerable<CensusItem> page in pages.WithCancellation(ct))
            items.AddRange(page);

        if (items.Count is 0)
            return null;

        _cache.Set(cacheKey, items, GetItemCacheOptions());
        CacheItems(items);
        return items;
    }

    private IQueryBuilder CreateDefaultItemQuery()
        => QueryService.CreateQuery()
            .OnCollection("item")
            .ShowFields("item_id", "item_category_id", "is_vehicle_weapon", "name", "faction_id")
            .WithLimit(1000);

    private void CacheItems(IEnumerable<CensusItem> items)
    {
        foreach (CensusItem item in items)
        {
            object cacheKey = GetItemCacheKey(item.ItemId);
            _cache.Set(cacheKey, item);
        }
    }

    private static object GetItemCacheKey(uint itemId)
        => (typeof(CensusItem), itemId);

    private static object GetCategoryBundleCacheKey(uint itemCategoryId)
        => (typeof(CensusItem), "CategoryBundle", itemCategoryId);

    private static MemoryCacheEntryOptions GetItemCacheOptions()
        => new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
            Priority = CacheItemPriority.Low
        };
}
