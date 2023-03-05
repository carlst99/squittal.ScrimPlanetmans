using System.Collections.Generic;
using squittal.ScrimPlanetmans.App.Data.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Models;

public class ConstructedTeamInfo
{
    public int? Id { get; set; }
    public string Name { get; set; }
    public string Alias { get; set; }

    public bool IsHiddenFromSelection { get; set; } = false;

    public IEnumerable<ConstructedTeamFactionPreference> FactionPreferences { get; set; }
    //public IEnumerable<Player> Players { get; set; } // For Scrim Match Display
    //public IEnumerable<Character> Characters { get; set; } // For Constructor Form Display
}
