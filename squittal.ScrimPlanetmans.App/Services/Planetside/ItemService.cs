using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.CensusServices;
using squittal.ScrimPlanetmans.App.CensusServices.Models;
using squittal.ScrimPlanetmans.App.Data.Interfaces;
using squittal.ScrimPlanetmans.App.Models.Planetside;
using squittal.ScrimPlanetmans.App.Services.Interfaces;
using squittal.ScrimPlanetmans.App.Services.Planetside.Interfaces;

namespace squittal.ScrimPlanetmans.App.Services.Planetside;

public class ItemService : IItemService
{
    private readonly IDbContextHelper _dbContextHelper;
    private readonly IItemCategoryService _itemCategoryService;
    private readonly CensusItem _censusItem;
    private readonly ISqlScriptRunner _sqlScriptRunner;
    private readonly ILogger<ItemService> _logger;

    private readonly ConcurrentDictionary<int, Item> _itemsMap = new();
    private readonly SemaphoreSlim _itemMapSetUpSemaphore = new(1);

    private readonly ConcurrentDictionary<int, Item> _weaponsMap = new();
    private readonly SemaphoreSlim _weaponMapSetUpSemaphore = new(1);

    public string BackupSqlScriptFileName => "CensusBackups\\dbo.Item.Table.sql";

    public event EventHandler<StoreRefreshMessageEventArgs>? RaiseStoreRefreshEvent;

    protected virtual void OnRaiseStoreRefreshEvent(StoreRefreshMessageEventArgs e)
    {
        RaiseStoreRefreshEvent?.Invoke(this, e);
    }

    private void SendStoreRefreshEventMessage(StoreRefreshSource refreshSource)
    {
        OnRaiseStoreRefreshEvent(new StoreRefreshMessageEventArgs(refreshSource));
    }

    public ItemService(IDbContextHelper dbContextHelper, IItemCategoryService itemCategoryService,
        CensusItem censusItem, ISqlScriptRunner sqlScriptRunner, ILogger<ItemService> logger)
    {
        _dbContextHelper = dbContextHelper;
        _itemCategoryService = itemCategoryService;
        _censusItem = censusItem;
        _sqlScriptRunner = sqlScriptRunner;
        _logger = logger;
    }

    public async Task<Item?> GetItemAsync(int itemId)
    {
        if (_itemsMap.IsEmpty)
            await SetUpItemsMapAsync();

        _itemsMap.TryGetValue(itemId, out Item? item);

        return item;
    }

    public async Task<IEnumerable<Item>> GetItemsByCategoryIdAsync(int categoryId)
    {
        if (_itemsMap.IsEmpty)
            await SetUpItemsMapAsync();

        return _itemsMap.Values
            .Where(i => i.ItemCategoryId == categoryId && i.ItemCategoryId.HasValue)
            .ToList();
    }

    public async Task SetUpItemsMapAsync()
    {
        await _itemMapSetUpSemaphore.WaitAsync();

        try
        {
            using var factory = _dbContextHelper.GetFactory();
            var dbContext = factory.GetDbContext();

            var storeItems = await dbContext.Items.ToListAsync();

            foreach (int itemId in _itemsMap.Keys)
            {
                if (storeItems.All(i => i.Id != itemId))
                    _itemsMap.TryRemove(itemId, out _);
            }

            foreach (Item item in storeItems)
            {
                if (_itemsMap.ContainsKey(item.Id))
                    _itemsMap[item.Id] = item;
                else
                    _itemsMap.TryAdd(item.Id, item);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up Items Map");
        }
        finally
        {
            _itemMapSetUpSemaphore.Release();
        }
    }

    public async Task<IEnumerable<Item>> GetAllWeaponItemsAsync()
    {
        if (_weaponsMap.IsEmpty)
        {
            await SetUpWeaponsMapAsync();
        }

        return _weaponsMap.Values.ToList();
    }

    public async Task<Item?> GetWeaponItemAsync(int id)
    {
        // TODO: handle "Unknown" weapon deaths/kills, like Fatalities

        if (_weaponsMap.IsEmpty)
        {
            await SetUpWeaponsMapAsync();
        }

        _weaponsMap.TryGetValue(id, out Item? item);

        return item;
    }

    public async Task SetUpWeaponsMapAsync()
    {
        await _weaponMapSetUpSemaphore.WaitAsync();

        try
        {
            using var factory = _dbContextHelper.GetFactory();
            var dbContext = factory.GetDbContext();

            var nonWeaponItemCategoryIds = _itemCategoryService.GetNonWeaponItemCategoryIds();

            List<Item> storeWeapons = await dbContext.Items
                .Where
                (
                    i => i.ItemCategoryId.HasValue
                        && !nonWeaponItemCategoryIds.Contains(i.ItemCategoryId.Value)
                )
                .ToListAsync();

            foreach (int weaponId in _weaponsMap.Keys)
            {
                if (storeWeapons.All(i => i.Id != weaponId))
                    _weaponsMap.TryRemove(weaponId, out _);
            }

            foreach (Item weapon in storeWeapons)
            {
                if (_weaponsMap.ContainsKey(weapon.Id))
                    _weaponsMap[weapon.Id] = weapon;
                else
                    _weaponsMap.TryAdd(weapon.Id, weapon);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up Weapons Map");
        }
        finally
        {
            _weaponMapSetUpSemaphore.Release();
        }
    }

    public async Task RefreshStore(bool onlyQueryCensusIfEmpty = false, bool canUseBackupScript = false)
    {
        if (onlyQueryCensusIfEmpty)
        {
            using var factory = _dbContextHelper.GetFactory();
            var dbContext = factory.GetDbContext();

            var anyItems = await dbContext.Items.AnyAsync();
            if (anyItems)
            {
                await SetUpWeaponsMapAsync();

                return;
            }
        }

        var success = await RefreshStoreFromCensus();

        if (!success && canUseBackupScript)
        {
            RefreshStoreFromBackup();
        }

        await SetUpWeaponsMapAsync();
    }

    public async Task<bool> RefreshStoreFromCensus()
    {
        List<CensusItemModel> items;

        try
        {
            items = (await _censusItem.GetAllWeaponItems())
                .ToList();
        }
        catch
        {
            _logger.LogError("Census API query failed: get all weapon Items. Refreshing store from backup...");
            return false;
        }

        if (items.Count is 0)
            return false;

        await UpsertRangeAsync(items.Select(ConvertToDbModel));
        _logger.LogInformation("Refreshed Items store: {ItemCount} entries", items.Count);
        SendStoreRefreshEventMessage(StoreRefreshSource.CensusApi);

        return true;
    }

    private async Task UpsertRangeAsync(IEnumerable<Item> censusEntities)
    {
        var createdEntities = new List<Item>();

        using (var factory = _dbContextHelper.GetFactory())
        {
            var dbContext = factory.GetDbContext();

            var storedEntities = await dbContext.Items.ToListAsync();

            foreach (var censusEntity in censusEntities)
            {
                var storeEntity = storedEntities.FirstOrDefault(e => e.Id == censusEntity.Id);
                if (storeEntity == null)
                {
                    createdEntities.Add(censusEntity);
                }
                else
                {
                    storeEntity = censusEntity;
                    dbContext.Items.Update(storeEntity);
                }
            }

            if (createdEntities.Any())
            {
                await dbContext.Items.AddRangeAsync(createdEntities);
            }

            await dbContext.SaveChangesAsync();
        }
    }

    private static Item ConvertToDbModel(CensusItemModel item)
    {
        return new Item
        {
            Id = item.ItemId,
            ItemTypeId = item.ItemTypeId,
            ItemCategoryId = item.ItemCategoryId,
            IsVehicleWeapon = item.IsVehicleWeapon,
            Name = item.Name?.English,
            Description = item.Description?.English,
            FactionId = item.FactionId,
            MaxStackSize = item.MaxStackSize,
            ImageId = item.ImageId
        };
    }

    public async Task<int> GetCensusCountAsync()
    {
        return await _censusItem.GetWeaponItemsCount();
    }

    public async Task<int> GetStoreCountAsync()
    {
        using var factory = _dbContextHelper.GetFactory();
        var dbContext = factory.GetDbContext();

        return await dbContext.Items.CountAsync();
    }

    public void RefreshStoreFromBackup()
    {
        _sqlScriptRunner.RunSqlScript(BackupSqlScriptFileName);

        SendStoreRefreshEventMessage(StoreRefreshSource.BackupSqlScript);
    }
}
