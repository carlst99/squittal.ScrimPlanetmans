using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DaybreakGames.Census;
using squittal.ScrimPlanetmans.App.CensusServices.Models;

namespace squittal.ScrimPlanetmans.App.CensusServices;

public class CensusFaction
{
    private readonly ICensusQueryFactory _queryFactory;

    public CensusFaction(ICensusQueryFactory queryFactory)
    {
        _queryFactory = queryFactory;
    }

    public async Task<IEnumerable<CensusFactionModel>> GetAllFactions()
    {
        var query = _queryFactory.Create("faction");
        query.SetLanguage("en");

        query.ShowFields("faction_id", "name", "image_id", "code_tag", "user_selectable");

        return await query.GetBatchAsync<CensusFactionModel>();
    }

    public async Task<int> GetFactionsCount()
    {
        var results = await GetAllFactions();
        return results.Count();
    }
}
