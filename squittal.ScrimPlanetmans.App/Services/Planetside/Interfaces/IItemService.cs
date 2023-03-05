using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.Models.Planetside;

namespace squittal.ScrimPlanetmans.Services.Planetside
{
    public interface IItemService : ILocallyBackedCensusStore
    {
        event EventHandler<StoreRefreshMessageEventArgs> RaiseStoreRefreshEvent;

        Task<IEnumerable<Item>> GetAllWeaponItemsAsync();
        Task<Item?> GetItemAsync(int itemId);
        Task<IEnumerable<Item>> GetItemsByCategoryIdAsync(int categoryId);
        Task<Item?> GetWeaponItemAsync(int id);

        Task SetUpItemsMapAsync();
        Task SetUpWeaponsMapAsync();
    }
}
