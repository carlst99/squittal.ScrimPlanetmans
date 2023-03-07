using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models.Planetside;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;
using squittal.ScrimPlanetmans.App.Services.Planetside.Interfaces;
using squittal.ScrimPlanetmans.App.Services.ScrimMatch.Interfaces;

namespace squittal.ScrimPlanetmans.App.Services.ScrimMatch;

public class ScrimPlayersService : IScrimPlayersService
{
    private readonly IOutfitService _outfits;
    private readonly ICharacterService _characters;

    public ScrimPlayersService(IOutfitService outfits, ICharacterService characters)
    {
        _outfits = outfits;
        _characters = characters;
    }
    public async Task<Player?> GetPlayerFromCharacterIdAsync(string characterId)
    {
        Character? character = await _characters.GetCharacterAsync(characterId);
        return character is null
            ? null
            : new Player(character);
    }

    public async Task<Player?> GetPlayerFromCharacterNameAsync(string characterName)
    {
        Character? character = await _characters.GetCharacterByNameAsync(characterName);
        return character is null
            ? null
            : new Player(character);
    }

    public async Task<IEnumerable<Player>?> GetPlayersFromOutfitAliasAsync(string alias)
    {
        IEnumerable<Character>? censusMembers = await _outfits.GetOutfitMembersByAliasAsync(alias);

        return censusMembers?
            .Where(m => m is { Id: {}, Name:{} })
            .Select(m => new Player(m))
            .Distinct();
    }
}
