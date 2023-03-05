using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DaybreakGames.Census;
using squittal.ScrimPlanetmans.App.CensusServices.Models;

namespace squittal.ScrimPlanetmans.App.CensusServices;

public class CensusWorld
{
    private readonly ICensusQueryFactory _queryFactory;

    public CensusWorld(ICensusQueryFactory queryFactory)
    {
        _queryFactory = queryFactory;
    }

    public async Task<IEnumerable<CensusWorldModel>> GetAllWorlds()
    {
        var query = _queryFactory.Create("world");
        query.SetLanguage("en");

        return await query.GetBatchAsync<CensusWorldModel>();
    }

    public async Task<int> GetWorldsCount()
    {
        var results = await GetAllWorlds();
        return results.Count();
    }
}
