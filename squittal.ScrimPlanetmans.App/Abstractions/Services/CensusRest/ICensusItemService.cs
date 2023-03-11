using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models.CensusRest;

namespace squittal.ScrimPlanetmans.App.Abstractions.Services.CensusRest;

public interface ICensusItemService
{
    Task<CensusItem?> GetByIdAsync(uint itemId, CancellationToken ct = default);
    Task<IReadOnlyList<CensusItem>> GetByIdAsync(IEnumerable<uint> itemIds, CancellationToken ct = default);
    Task<CensusItem?> GetWeaponAsync(uint weaponItemId, CancellationToken ct = default);
    Task<IReadOnlyList<CensusItem>?> GetAllWeaponsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CensusItem>?> GetByCategoryAsync(uint itemCategoryId, CancellationToken ct = default);
}
