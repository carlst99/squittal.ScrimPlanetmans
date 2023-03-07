using System.Collections.Generic;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.Services.ScrimMatch.Interfaces;

public interface IScrimPlayersService
{
    Task<Player?> GetPlayerFromCharacterIdAsync(string characterId);
    Task<Player?> GetPlayerFromCharacterNameAsync(string characterName);
    Task<IEnumerable<Player>?> GetPlayersFromOutfitAliasAsync(string alias);
}
