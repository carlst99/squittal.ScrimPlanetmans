using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.ScrimMatch.Events;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Interfaces;

public interface IScrimMatchEngine
{
    MatchConfiguration MatchConfiguration { get; set; }
    Ruleset.Models.Ruleset MatchRuleset { get; }

    Task Start();
    Task InitializeNewMatch();
    void ConfigureMatch(MatchConfiguration configuration);
    Task InitializeNewRound();
    void StartRound();
    void PauseRound();
    void ResumeRound();
    Task EndRound();
    Task ResetRound();
    Task ClearMatch(bool isRematch);
    MatchTimerTickMessage GetLatestTimerTickMessage();
    bool IsRunning();
    int GetCurrentRound();
    MatchState GetMatchState();

    void SubmitPlayersList();
    string GetMatchId();
}
