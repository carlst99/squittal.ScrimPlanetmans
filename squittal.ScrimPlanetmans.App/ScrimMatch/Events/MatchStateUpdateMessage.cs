using System;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public class MatchStateUpdateMessage
{
    public string MatchId { get; set; }
    public MatchState MatchState { get; set; }
    public DateTime Timestamp { get; set; }
    public int CurrentRound { get; set; }
    public string MatchTitle { get; set; }
    public string Info { get; set; }

    public MatchStateUpdateMessage(MatchState matchState, int currentRound, DateTime timestamp, string matchTitle, string matchId)
    {
        MatchId = matchId;
        MatchState = matchState;
        Timestamp = timestamp;
        CurrentRound = currentRound;
        MatchTitle = matchTitle;

        Info = $"Match \"{MatchTitle}\" [{MatchId}] State Changed: {Enum.GetName(typeof(MatchState), matchState).ToUpper()} | Round {CurrentRound}";
    }
}
