using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DaybreakGames.Census;
using squittal.ScrimPlanetmans.App.CensusServices.Models;

namespace squittal.ScrimPlanetmans.App.CensusServices;

public class CensusProfile
{
    private readonly ICensusQueryFactory _queryFactory;

    public CensusProfile(ICensusQueryFactory queryFactory)
    {
        _queryFactory = queryFactory;
    }

    public async Task<IEnumerable<CensusProfileModel>> GetAllProfilesAsync()
    {
        var query = _queryFactory.Create("profile");
        query.SetLanguage("en");

        query.ShowFields("profile_id", "profile_type_id", "faction_id", "name", "image_id");

        return await query.GetBatchAsync<CensusProfileModel>();
    }

    public async Task<int> GetProfilesCount()
    {
        var results = await GetAllProfilesAsync();
        return results.Count();
    }
}
