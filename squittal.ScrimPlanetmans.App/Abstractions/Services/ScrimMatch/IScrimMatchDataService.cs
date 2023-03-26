using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.Abstractions.Services.ScrimMatch;

public interface IScrimMatchDataService
{
    string CurrentMatchId { get; set; }
    int CurrentMatchRound { get; set; }
    int CurrentMatchRulesetId { get; set; }

    Task SaveToCurrentMatchAsync(Data.Models.ScrimMatch scrimMatch, CancellationToken ct = default);

    Task SaveCurrentMatchRoundConfigurationAsync(MatchConfiguration matchConfiguration, CancellationToken ct = default);
    Task RemoveMatchRoundConfigurationAsync(int roundToDelete, CancellationToken ct = default);


    Task<Data.Models.ScrimMatch?> GetCurrentMatchAsync(CancellationToken ct = default);

    IEnumerable<Data.Models.ScrimMatch> GetAllMatches();
    Task<bool> TryRemoveMatchParticipatingPlayerAsync(ulong characterId, CancellationToken ct = default);
    Task SaveMatchParticipatingPlayerAsync(Player player, CancellationToken ct = default);
}
