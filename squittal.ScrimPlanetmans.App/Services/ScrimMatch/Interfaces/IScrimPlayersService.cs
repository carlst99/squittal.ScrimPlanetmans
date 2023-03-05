using System.Collections.Generic;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.Services.ScrimMatch.Interfaces;

public interface IScrimPlayersService
{
    Task<Player> GetPlayerFromCharacterId(string characterId);
    Task<Player> GetPlayerFromCharacterName(string characterName);
    Task<IEnumerable<Player>> GetPlayersFromOutfitAlias(string alias);
}
