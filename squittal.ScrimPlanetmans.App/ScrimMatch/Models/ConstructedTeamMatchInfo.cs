using squittal.ScrimPlanetmans.App.Data.Models;
using squittal.ScrimPlanetmans.App.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Models;

public class ConstructedTeamMatchInfo
{
    public ConstructedTeam? ConstructedTeam { get; init; }

    public TeamDefinition TeamOrdinal { get; set; }
    public int ActiveFactionId { get; set; }

    public int? MembersOnlineCount { get; set; } // = 0;
    public int? MembersFactionCount { get; set; } // = 0;

    //public IEnumerable<Player> Players { get; set; }
}
