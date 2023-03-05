using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models.Planetside;
using squittal.ScrimPlanetmans.App.Services.Interfaces;

namespace squittal.ScrimPlanetmans.App.Services.Planetside.Interfaces;

public interface IItemCategoryService : ILocallyBackedCensusStore
{
    event EventHandler<StoreRefreshMessageEventArgs> RaiseStoreRefreshEvent;

    Task<IEnumerable<ItemCategory>> GetItemCategoriesFromIdsAsync(IEnumerable<int> itemCategoryIds);
    Task<IEnumerable<int>> GetItemCategoryIdsAsync();
    IEnumerable<int> GetNonWeaponItemCategoryIds();
    IEnumerable<ItemCategory> GetWeaponItemCategories();
    ItemCategory? GetWeaponItemCategory(int itemCategoryId);
    Task<ItemCategory?> GetWeaponItemCategoryAsync(int itemCategoryId);
    Task<IEnumerable<int>> GetWeaponItemCategoryIdsAsync();
    Task SetUpWeaponCategoriesListAsync();
}
