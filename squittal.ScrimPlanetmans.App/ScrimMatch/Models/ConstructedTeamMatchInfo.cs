using squittal.ScrimPlanetmans.App.Data.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Models;

public class ConstructedTeamMatchInfo
{
    public ConstructedTeam ConstructedTeam { get; set; }

    public int TeamOrdinal { get; set; }
    public int ActiveFactionId { get; set; }

    public int? MembersOnlineCount { get; set; } // = 0;
    public int? MembersFactionCount { get; set; } // = 0;
    public int TotalMembersCount { get; set; } = 0;

    //public IEnumerable<Player> Players { get; set; }
}
