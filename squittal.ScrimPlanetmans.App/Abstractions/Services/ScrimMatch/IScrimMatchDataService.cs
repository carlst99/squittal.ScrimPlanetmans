using System.Collections.Generic;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.Abstractions.Services.ScrimMatch;

public interface IScrimMatchDataService
{
    string CurrentMatchId { get; set; }
    int CurrentMatchRound { get; set; }
    int CurrentMatchRulesetId { get; set; }

    Task SaveToCurrentMatch(Data.Models.ScrimMatch scrimMatch);

    Task SaveCurrentMatchRoundConfiguration(MatchConfiguration matchConfiguration);
    Task RemoveMatchRoundConfiguration(int roundToDelete);


    Task<Data.Models.ScrimMatch> GetCurrentMatch();

    IEnumerable<Data.Models.ScrimMatch> GetAllMatches();
    Task<bool> TryRemoveMatchParticipatingPlayer(ulong characterId);
    Task SaveMatchParticipatingPlayer(Player player);
}
