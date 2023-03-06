using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public class MatchTimerTickMessage
{
    public MatchTimerStatus MatchTimerStatus { get; set; }

    public string Info { get; set; } = string.Empty;

    public MatchTimerTickMessage(MatchTimerStatus matchTimerStatus)
    {
        MatchTimerStatus = matchTimerStatus;
    }
}
