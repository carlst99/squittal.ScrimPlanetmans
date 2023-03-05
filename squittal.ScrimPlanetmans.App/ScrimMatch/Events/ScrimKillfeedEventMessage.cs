using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public class ScrimKillfeedEventMessage
{
    public ScrimKillfeedEvent KillfeedEvent { get; set; }

    public ScrimKillfeedEventMessage(ScrimKillfeedEvent killfeedEvent)
    {
        KillfeedEvent = killfeedEvent;
    }
}
