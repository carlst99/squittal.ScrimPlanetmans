using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models.CensusRest;

namespace squittal.ScrimPlanetmans.App.Abstractions.Services.CensusRest;

public interface ICensusItemCategoryService
{
    Task<IReadOnlyList<CensusItemCategory>?> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<CensusItemCategory>?> GetByIdAsync(IEnumerable<uint> categoryIds, CancellationToken ct = default);
    Task<IEnumerable<CensusItemCategory>?> GetAllWeaponCategoriesAsync(CancellationToken ct = default);
}
