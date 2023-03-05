using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DaybreakGames.Census;
using squittal.ScrimPlanetmans.App.CensusServices.Models;

namespace squittal.ScrimPlanetmans.App.CensusServices;

public class CensusItemCategory
{
    private readonly ICensusQueryFactory _queryFactory;

    public CensusItemCategory(ICensusQueryFactory queryFactory)
    {
        _queryFactory = queryFactory;
    }

    public async Task<IEnumerable<CensusItemCategoryModel>> GetAllItemCategories()
    {
        var query = _queryFactory.Create("item_category");
        query.SetLanguage("en");

        query.ShowFields("item_category_id", "name");

        return await query.GetBatchAsync<CensusItemCategoryModel>();
    }

    public async Task<int> GetItemCategoriesCount()
    {
        var results = await GetAllItemCategories();

        return results.Count();
    }
}
