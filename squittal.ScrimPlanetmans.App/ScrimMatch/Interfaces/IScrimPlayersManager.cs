using System.Collections.Generic;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Interfaces;

public interface IScrimPlayersManager
{
    Task<Player> GetPlayerFromCharacterId(string characterId);
    Task<IEnumerable<Player>> GetPlayersFromOutfitAlias(string alias);
}
