using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusRest;
using squittal.ScrimPlanetmans.App.Abstractions.Services.Planetside;
using squittal.ScrimPlanetmans.App.Models.CensusRest;
using squittal.ScrimPlanetmans.App.Models.Planetside;

namespace squittal.ScrimPlanetmans.App.Services.Planetside;

public class ItemCategoryService : IItemCategoryService
{
    private readonly ICensusItemCategoryService _censusItemCategory;

    private static readonly HashSet<uint> _nonWeaponItemCategoryIds = new()
    {
        99,  // Camo
        100, // Infantry
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

    private static readonly HashSet<uint> _infantryItemCategoryIds = new()
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
        157, // Hybrid Rifle
        219, // Heavy Crossbow
        220, // Amphibious Rifle
        223, // Amphibious Sidearm
        224  // Anti-Materiel Rifle
    };

    private static readonly HashSet<uint> _maxItemCategoryIds = new()
    {
        9,  // AV MAX (Left)
        10, // AI MAX (Left)
        16, // Flak MAX
        20, // AA MAX (Right)
        21, // AV MAX (Right)
        22, // AI MAX (Right)
        23  // AA MAX (Left)
    };

    private static readonly HashSet<uint> _groundVehicleItemCategoryIds = new()
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
        144, // ANT Top Turret
        211, // Colossus Primary Weapon
        212, // Colossus Front Right Weapon
        213, // Colossus Front Left Weapon
        214, // Colossus Rear Right Weapon
        215, // Colossus Rear Left Weapon
        216, // Javelin Primary Weapon
        217, // Chimera Primary Weapons
        218  // Chimera Secondary Weapon
    };

    private static readonly HashSet<uint> _airVehicleItemCategoryIds = new()
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
        138, // Valkyrie Nose Gunner,
        209, // Bastion Bombard
        210  // Bastion Weapon System
    };

    private static readonly HashSet<uint> _aquaticVehicleItemCategoryIds = new()
    {
        221, // Corsair Front Turret
        222  // Corsair Rear Turret
    };

    private static readonly HashSet<uint> _lockedItemCategoryIds = new()
    {
        15  // Flamethrower MAX
    };

    public ItemCategoryService(ICensusItemCategoryService censusItemCategory)
    {
        _censusItemCategory = censusItemCategory;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ItemCategory>?> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<CensusItemCategory>? categories = await _censusItemCategory.GetAllAsync(ct);
        return categories?.Select(ConvertToInternalModel);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ItemCategory>?> GetItemCategoriesFromIdsAsync
    (
        IEnumerable<uint> itemCategoryIds,
        CancellationToken ct = default
    )
    {
        IEnumerable<CensusItemCategory>? censusCategories = await _censusItemCategory.GetByIdAsync(itemCategoryIds, ct);
        return censusCategories?.Select(ConvertToInternalModel);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<uint>?> GetWeaponItemCategoryIdsAsync(CancellationToken ct = default)
    {
        IEnumerable<CensusItemCategory>? weaponCategories = await _censusItemCategory.GetAllWeaponCategoriesAsync(ct);
        return weaponCategories?.Select(x => x.ItemCategoryId);
    }

    private static ItemCategory ConvertToInternalModel(CensusItemCategory itemCategory)
    {
        uint id = itemCategory.ItemCategoryId;

        bool isWeaponCategory = GetIsWeaponItemCategory(id);
        ItemCategoryDomain domain = GetItemCategoryDomain(id);

        string name = itemCategory.Name.English.HasValue
            ? itemCategory.Name.English.Value
            : $"Category {id}";

        return new ItemCategory(id, name, isWeaponCategory, domain);
    }

    private static bool GetIsWeaponItemCategory(uint itemCategoryId)
        => !_nonWeaponItemCategoryIds.Contains(itemCategoryId);

    private static ItemCategoryDomain GetItemCategoryDomain(uint itemCategoryId)
    {
        if (_lockedItemCategoryIds.Contains(itemCategoryId))
            return ItemCategoryDomain.Locked;

        if (_maxItemCategoryIds.Contains(itemCategoryId))
            return ItemCategoryDomain.Max;

        if (_infantryItemCategoryIds.Contains(itemCategoryId))
            return ItemCategoryDomain.Infantry;

        if (_groundVehicleItemCategoryIds.Contains(itemCategoryId))
            return ItemCategoryDomain.GroundVehicle;

        if (_airVehicleItemCategoryIds.Contains(itemCategoryId))
            return ItemCategoryDomain.AirVehicle;

        if (_aquaticVehicleItemCategoryIds.Contains(itemCategoryId))
            return ItemCategoryDomain.AquaticVehicle;

        return ItemCategoryDomain.Other;
    }
}
