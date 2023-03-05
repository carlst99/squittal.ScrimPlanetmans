using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DaybreakGames.Census;
using squittal.ScrimPlanetmans.App.CensusServices.Models;

namespace squittal.ScrimPlanetmans.App.CensusServices;

public class CensusLoadout
{
    private readonly ICensusQueryFactory _queryFactory;

    public CensusLoadout(ICensusQueryFactory queryFactory)
    {
        _queryFactory = queryFactory;
    }

    public async Task<IEnumerable<CensusLoadoutModel>> GetAllLoadoutsAsync()
    {
        var query = _queryFactory.Create("loadout");

        query.ShowFields("loadout_id", "profile_id", "faction_id", "code_name");

        return await query.GetBatchAsync<CensusLoadoutModel>();
    }

    public async Task<int> GetLoadoutsCount()
    {
        var results = await GetAllLoadoutsAsync();
        return results.Count();
    }
}
