using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DaybreakGames.Census;
using squittal.ScrimPlanetmans.App.CensusServices.Models;

namespace squittal.ScrimPlanetmans.App.CensusServices;

public class CensusVehicle
{
    private readonly ICensusQueryFactory _queryFactory;

    public CensusVehicle(ICensusQueryFactory queryFactory)
    {
        _queryFactory = queryFactory;
    }

    public async Task<IEnumerable<CensusVehicleModel>> GetAllVehicles()
    {
        var query = _queryFactory.Create("vehicle");
        query.SetLanguage("en");

        query.ShowFields("vehicle_id", "name", "description", "type_id", "type_name", "cost", "cost_resource_id", "image_id");

        return await query.GetBatchAsync<CensusVehicleModel>();
    }

    public async Task<int> GetVehiclesCount()
    {
        var results = await GetAllVehicles();

        return results.Count();
    }
}
