using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public class MatchConfigurationUpdateMessage
{
    public MatchConfiguration MatchConfiguration { get; set; }
    public string Info { get; set; }

    public MatchConfigurationUpdateMessage(MatchConfiguration matchConfiguration)
    {
        MatchConfiguration = matchConfiguration;

        Info = "Match Configuration updated";
    }
}
