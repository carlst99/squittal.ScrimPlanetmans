using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models.Planetside;

namespace squittal.ScrimPlanetmans.App.Services.Planetside.Interfaces;

public interface IItemCategoryService
{
    Task<IEnumerable<ItemCategory>?> GetAllAsync(CancellationToken ct = default);

    Task<IEnumerable<ItemCategory>?> GetItemCategoriesFromIdsAsync
    (
        IEnumerable<uint> itemCategoryIds,
        CancellationToken ct = default
    );

    Task<IEnumerable<uint>?> GetWeaponItemCategoryIdsAsync(CancellationToken ct = default);
}
