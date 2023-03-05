using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.CensusServices;
using squittal.ScrimPlanetmans.CensusServices.Models;
using squittal.ScrimPlanetmans.Data;
using squittal.ScrimPlanetmans.Models.Planetside;

namespace squittal.ScrimPlanetmans.Services.Planetside;

public class ItemCategoryService : IItemCategoryService
{
    private readonly IDbContextHelper _dbContextHelper;
    private readonly CensusItemCategory _censusItemCategory;
    private readonly ISqlScriptRunner _sqlScriptRunner;
    private readonly ILogger<ItemCategoryService> _logger;

    private readonly ConcurrentDictionary<int, ItemCategory> _itemCategoriesMap = new();
    private readonly SemaphoreSlim _itemCategoryMapSetUpSemaphore = new(1);

    private readonly ConcurrentDictionary<int, ItemCategory> _weaponCategoriesMap = new();
    private readonly SemaphoreSlim _weaponCategoryMapSetUpSemaphore = new(1);
    public string BackupSqlScriptFileName => "CensusBackups\\dbo.ItemCategory.Table.sql";

    public event EventHandler<StoreRefreshMessageEventArgs>? RaiseStoreRefreshEvent;

    protected virtual void OnRaiseStoreRefreshEvent(StoreRefreshMessageEventArgs e)
    {
        RaiseStoreRefreshEvent?.Invoke(this, e);
    }

    private void SendStoreRefreshEventMessage(StoreRefreshSource refreshSource)
    {
        OnRaiseStoreRefreshEvent(new StoreRefreshMessageEventArgs(refreshSource));
    }

    private static readonly int[] _nonWeaponItemCategoryIds =
    {
        99,  // Camo
        101, // Vehicles
        103, // Infantry Gear
        105, // Vehicle Gear
        106, // Armor Camo
        107, // Weapon Camo
        108, // Vehicle Camo
        133, // Implants
        134, // Consolidated Camo
        135, // VO Packs
        136, // Male VO Pack
        137, // Female VO Pack
        139, // Infantry Abilities
        140, // Vehicle Abilities
        141, // Boosts & Utilities
        142, // Consolidated Decal
        143, // Attachments
        145, // ANT Utility
        146, // Bounty Contract
        148  // ANT Harvesting Tool
    };

    private static readonly int[] _infantryItemCategoryIds =
    {
        2,   // Knife
        3,   // Pistol
        4,   // Shotgun,
        5,   // SMG
        6,   // LMG
        7,   // Assault Rifle
        8,   // Carbine
        11,  // Sniper Rifle
        12,  // Scout Rifle
        13,  // Rocket Launcher
        14,  // Heavy Weapon
        17,  // Grenade
        18,  // Explosive
        19,  // Battle Rifle
        24,  // Crossbow
        100, // Infantry
        102, // Infantry Weapons
        147, // Aerial Combat Weapon (i.e. rocklet rifles)
        157  // Hybrid Rifle
    };

    private static readonly int[] _maxItemCategoryIds =
    {
        9,  // AV MAX (Left)
        10, // AI MAX (Left)
        16, // Flak MAX
        20, // AA MAX (Right)
        21, // AV MAX (Right)
        22, // AI MAX (Right)
        23  // AA MAX (Left)
    };

    private static readonly int[] _groundVehicleItemCategoryIds =
    {
        109, // Flash Primary Weapon
        114, // Harasser Top Gunner
        118, // Lightning Primary Weapon
        119, // Magrider Gunner Weapon
        120, // Magrider Primary Weapon
        123, // Prowler Gunner Weapon
        124, // Prowler Primary Weapon
        129, // Sunderer Front Gunner
        130, // Sunderer Rear Gunner
        131, // Vanguard Gunner Weapon
        132, // Vanguard Primary Weapon
        144  // ANT Top Turret
    };

    private static readonly int[] _airVehicleItemCategoryIds =
    {
        110, // Galaxy Left Weapon
        111, // Galaxy Tail Weapon
        112, // Galaxy Right Weapon
        113, // Galaxy Top Weapon
        115, // Liberator Belly Weapon
        116, // Liberator Nose Cannon
        117, // Liberator Tail Weapon
        121, // Mosquito Nose Cannon
        122, // Mosquito Wing Mount
        125, // Reaver Nose Cannon
        126, // Reaver Wing Mount
        127, // Scythe Nose Cannon
        128, // Scythe Wing Mount
        138  // Valkyrie Nose Gunner
    };

    private static readonly int[] _lockedItemCategoryIds =
    {
        15  // Flamethrower MAX
    };

    public ItemCategoryService
    (
        IDbContextHelper dbContextHelper,
        CensusItemCategory censusItemCategory,
        ISqlScriptRunner sqlScriptRunner,
        ILogger<ItemCategoryService> logger
    )
    {
        _dbContextHelper = dbContextHelper;
        _censusItemCategory = censusItemCategory;
        _sqlScriptRunner = sqlScriptRunner;
        _logger = logger;
    }

    #region GET methods
    public async Task<IEnumerable<int>> GetItemCategoryIdsAsync()
    {
        if (_itemCategoriesMap.IsEmpty)
            await SetUpItemCategoriesListAsync();

        return GetItemCategoryIds();
    }

    public async Task<IEnumerable<int>> GetWeaponItemCategoryIdsAsync()
    {
        if (_weaponCategoriesMap.IsEmpty)
            await SetUpWeaponCategoriesListAsync();

        return GetWeaponItemCategoryIds();
    }

    public async Task<IEnumerable<ItemCategory>> GetItemCategoriesFromIdsAsync(IEnumerable<int> itemCategoryIds)
    {
        if (_itemCategoriesMap.IsEmpty)
            await SetUpItemCategoriesListAsync();

        List<ItemCategory> categories = new();
        foreach (int id in itemCategoryIds)
        {
            if (_itemCategoriesMap.TryGetValue(id, out ItemCategory? category))
                categories.Add(category);
        }

        return categories;
    }

    public IEnumerable<int> GetItemCategoryIds()
        => _itemCategoriesMap.Keys;

    public IEnumerable<int> GetWeaponItemCategoryIds()
        => _weaponCategoriesMap.Keys;

    public IEnumerable<ItemCategory> GetWeaponItemCategories()
        => _weaponCategoriesMap.Values;

    public async Task<ItemCategory?> GetWeaponItemCategoryAsync(int itemCategoryId)
    {
        if (_weaponCategoriesMap.IsEmpty)
            await SetUpWeaponCategoriesListAsync();

        return GetWeaponItemCategory(itemCategoryId);
    }

    public ItemCategory? GetWeaponItemCategory(int itemCategoryId)
    {
        _weaponCategoriesMap.TryGetValue(itemCategoryId, out ItemCategory? category);

        return category;
    }
    #endregion GET methods

    public async Task SetUpItemCategoriesListAsync()
    {
        await _itemCategoryMapSetUpSemaphore.WaitAsync();

        try
        {
            using var factory = _dbContextHelper.GetFactory();
            var dbContext = factory.GetDbContext();

            var storeCategories = await dbContext.ItemCategories.ToListAsync();

            foreach (var categoryId in _itemCategoriesMap.Keys)
            {
                if (storeCategories.All(c => c.Id != categoryId))
                    _itemCategoriesMap.TryRemove(categoryId, out _);
            }

            foreach (var category in storeCategories)
            {
                if (_itemCategoriesMap.ContainsKey(category.Id))
                {
                    _itemCategoriesMap[category.Id] = category;
                }
                else
                {
                    _itemCategoriesMap.TryAdd(category.Id, category);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up Item Categories Map");
        }
        finally
        {
            _itemCategoryMapSetUpSemaphore.Release();
        }
    }

    public async Task SetUpWeaponCategoriesListAsync()
    {
        await _weaponCategoryMapSetUpSemaphore.WaitAsync();

        try
        {
            using var factory = _dbContextHelper.GetFactory();
            var dbContext = factory.GetDbContext();

            var storeCategories = await dbContext.ItemCategories.ToListAsync();

            foreach (var categoryId in _weaponCategoriesMap.Keys)
            {
                if (storeCategories.All(c => c.Id != categoryId))
                {
                    _weaponCategoriesMap.TryRemove(categoryId, out _);
                }
            }

            foreach (var category in storeCategories)
            {
                if (_weaponCategoriesMap.ContainsKey(category.Id))
                {
                    _weaponCategoriesMap[category.Id] = category;
                }
                else
                {
                    _weaponCategoriesMap.TryAdd(category.Id, category);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up Weapon Item Categories Map");
        }
        finally
        {
            _weaponCategoryMapSetUpSemaphore.Release();
        }
    }

    public IEnumerable<int> GetNonWeaponItemCategoryIds()
    {
        return _nonWeaponItemCategoryIds;
    }

    #region Store Management methods
    public async Task RefreshStore(bool onlyQueryCensusIfEmpty = false, bool canUseBackupScript = false)
    {
        if (onlyQueryCensusIfEmpty)
        {
            using var factory = _dbContextHelper.GetFactory();
            var dbContext = factory.GetDbContext();

            bool anyCategories = await dbContext.ItemCategories.AnyAsync();

            if (anyCategories)
            {
                var categoriesToBackfill = await dbContext.ItemCategories
                    .Where(ic => ic.Domain == ItemCategoryDomain.Default)
                    .ToListAsync();

                if (!categoriesToBackfill.Any())
                {
                    return;
                }

                foreach (var category in categoriesToBackfill)
                {
                    category.IsWeaponCategory = GetIsWeaponItemCategory(category.Id);
                    category.Domain = GetItemCategoryDomain(category.Id);
                }

                await UpsertRangeAsync(categoriesToBackfill);

                _logger.LogInformation("Backfilled Item Categories store: {Amount} entries updated", categoriesToBackfill.Count);

                await SetUpWeaponCategoriesListAsync();

                return;
            }
        }

        var success = await RefreshStoreFromCensus();

        if (!success && canUseBackupScript)
        {
            RefreshStoreFromBackup();
        }

        await SetUpWeaponCategoriesListAsync();
    }

    public async Task<bool> RefreshStoreFromCensus()
    {
        List<CensusItemCategoryModel> itemCategories;

        try
        {
            itemCategories = (await _censusItemCategory.GetAllItemCategories()).ToList();
        }
        catch
        {
            _logger.LogError("Census API query failed: get all Item Categories. Refreshing store from backup...");
            return false;
        }

        if (!itemCategories.Any())
            return false;

        await UpsertRangeAsync(itemCategories.Select(ConvertToDbModel));

        _logger.LogInformation("Refreshed Item Categories store: {Amount} entries", itemCategories.Count);

        SendStoreRefreshEventMessage(StoreRefreshSource.CensusApi);

        return true;
    }

    private async Task UpsertRangeAsync(IEnumerable<ItemCategory> censusEntities)
    {
        var createdEntities = new List<ItemCategory>();

        using (var factory = _dbContextHelper.GetFactory())
        {
            var dbContext = factory.GetDbContext();

            var storedEntities = await dbContext.ItemCategories.ToListAsync();

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
                    dbContext.ItemCategories.Update(storeEntity);
                }
            }

            if (createdEntities.Any())
            {
                await dbContext.ItemCategories.AddRangeAsync(createdEntities);
            }

            await dbContext.SaveChangesAsync();
        }
    }

    private static ItemCategory ConvertToDbModel(CensusItemCategoryModel itemCategory)
    {
        var id = itemCategory.ItemCategoryId;

        var isWeaponCategory = GetIsWeaponItemCategory(id);
        var domain = GetItemCategoryDomain(id);

        return new ItemCategory
        {
            Id = id,
            Name = itemCategory.Name.English,
            IsWeaponCategory = isWeaponCategory,
            Domain = domain
        };
    }

    private static bool GetIsWeaponItemCategory(int itemCategoryId)
        => !_nonWeaponItemCategoryIds.Contains(itemCategoryId);

    private static ItemCategoryDomain GetItemCategoryDomain(int itemCategoryId)
    {
        if (_infantryItemCategoryIds.Contains(itemCategoryId))
        {
            return ItemCategoryDomain.Infantry;
        }
        else if (_maxItemCategoryIds.Contains(itemCategoryId))
        {
            return ItemCategoryDomain.Max;
        }
        else if (_groundVehicleItemCategoryIds.Contains(itemCategoryId))
        {
            return ItemCategoryDomain.GroundVehicle;
        }
        else if (_airVehicleItemCategoryIds.Contains(itemCategoryId))
        {
            return ItemCategoryDomain.AirVehicle;
        }
        else if (_lockedItemCategoryIds.Contains(itemCategoryId))
        {
            return ItemCategoryDomain.Locked;
        }
        else
        {
            return ItemCategoryDomain.Other;
        }
    }

    public async Task<int> GetCensusCountAsync()
    {
        return await _censusItemCategory.GetItemCategoriesCount();
    }

    public async Task<int> GetStoreCountAsync()
    {
        using var factory = _dbContextHelper.GetFactory();
        var dbContext = factory.GetDbContext();

        return await dbContext.ItemCategories.CountAsync();
    }

    public void RefreshStoreFromBackup()
    {
        _sqlScriptRunner.RunSqlScript(BackupSqlScriptFileName);

        SendStoreRefreshEventMessage(StoreRefreshSource.BackupSqlScript);
    }

    #endregion Store Management methods
}
