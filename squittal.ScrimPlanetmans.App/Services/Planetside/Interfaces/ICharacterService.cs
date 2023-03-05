using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models.Planetside;

namespace squittal.ScrimPlanetmans.App.Services.Planetside.Interfaces;

public interface ICharacterService
{
    Task<Character> GetCharacterAsync(string characterId);
    Task<Character> GetCharacterByNameAsync(string characterName);
    Task<OutfitMember> GetCharacterOutfitAsync(string characterId);
}
