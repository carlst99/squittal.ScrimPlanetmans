using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DaybreakGames.Census;
using DaybreakGames.Census.Operators;
using squittal.ScrimPlanetmans.App.CensusServices.Models;

namespace squittal.ScrimPlanetmans.App.CensusServices;

public class CensusOutfit
{
    private readonly ICensusQueryFactory _queryFactory;

    public CensusOutfit(ICensusQueryFactory queryFactory)
    {
        _queryFactory = queryFactory;
    }

    public async Task<CensusOutfitModel?> GetOutfitAsync(string outfitId)
    {
        CensusQuery? query = _queryFactory.Create("outfit");

        query.ShowFields("outfit_id", "name", "alias", "alias_lower", "time_created", "leader_character_id", "member_count");

        query.Where("outfit_id").Equals(outfitId);

        return await query.GetAsync<CensusOutfitModel>();
    }

    public async Task<CensusOutfitModel?> GetOutfitByAliasAsync(string alias)
    {
        CensusQuery? query = _queryFactory.Create("outfit");

        query.ShowFields("outfit_id", "name", "alias", "alias_lower", "time_created", "leader_character_id", "member_count");

        query.Where("alias_lower").Equals(alias.ToLower());

        return await query.GetAsync<CensusOutfitModel>();
    }

    public async Task<IEnumerable<CensusOutfitMemberCharacterModel>?> GetOutfitMembersAsync(string outfitId)
    {
        CensusQuery? query = _queryFactory.Create("outfit");

        query.ShowFields("outfit_id", "name", "alias", "alias_lower", "member_count");

        query.Where("outfit_id").Equals(outfitId);

        //query.AddResolve("member_character_name");
        query.AddResolve("member_character(name,prestige_level)");
        query.AddResolve("member_online_status");

        CensusOutfitResolveMemberCharacterModel? result = await query.GetAsync<CensusOutfitResolveMemberCharacterModel>();

        return result?.Members;
    }

    public async Task<IEnumerable<CensusOutfitMemberCharacterModel>?> GetOutfitMembersByAliasAsync(string alias)
    {
        CensusQuery? query = _queryFactory.Create("outfit");

        query.ShowFields("outfit_id", "name", "alias", "alias_lower", "member_count");

        query.Where("alias_lower").Equals(alias.ToLower());

        //query.AddResolve("member_character_name");
        query.AddResolve("member_character(name,prestige_level)");
        query.AddResolve("member_online_status");

        CensusOutfitResolveMemberCharacterModel? result = await query.GetAsync<CensusOutfitResolveMemberCharacterModel>();

        return result.Members.Select(m => FillOutOutfitMemberCharacterModel(m, result));
    }

    private static CensusOutfitMemberCharacterModel FillOutOutfitMemberCharacterModel
    (
        CensusOutfitMemberCharacterModel character,
        CensusOutfitModel outfit
    )
    {
        return new CensusOutfitMemberCharacterModel
        {
            OutfitId = outfit.OutfitId,
            CharacterId = character.CharacterId,
            Name = character.Name,
            OnlineStatus = character.OnlineStatus,
            OutfitAlias = outfit.Alias,
            OutfitAliasLower = outfit.AliasLower,
            PrestigeLevel = character.PrestigeLevel
        };
    }
}
