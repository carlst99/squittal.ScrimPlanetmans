using System.Collections.Generic;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models.Planetside;

namespace squittal.ScrimPlanetmans.App.Services.Planetside.Interfaces;

public interface IOutfitService
{
    Task<Outfit?> GetOutfitAsync(string outfitId);
    Task<Outfit?> GetOutfitByAliasAsync(string alias);
    Task<IEnumerable<Character>?> GetOutfitMembersByAliasAsync(string alias);
    Task<OutfitMember?> UpdateCharacterOutfitMembershipAsync(Character character);

}
