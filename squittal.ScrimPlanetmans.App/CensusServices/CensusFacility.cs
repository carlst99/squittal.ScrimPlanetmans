using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DaybreakGames.Census;
using squittal.ScrimPlanetmans.App.CensusServices.Models;

namespace squittal.ScrimPlanetmans.App.CensusServices;

public class CensusFacility
{
    public readonly ICensusQueryFactory _queryFactory;

    public CensusFacility(ICensusQueryFactory queryFactory)
    {
        _queryFactory = queryFactory;
    }

    public async Task<IEnumerable<CensusMapRegionModel>> GetAllMapRegions()
    {
        var query = _queryFactory.Create("map_region");

        query.ShowFields("map_region_id", "zone_id", "facility_id", "facility_name", "facility_type_id", "facility_type");

        return await query.GetBatchAsync<CensusMapRegionModel>();
    }

    public async Task<int> GetMapRegionsCount()
    {
        var results = await GetAllMapRegions();

        return results.Count();
    }
}
