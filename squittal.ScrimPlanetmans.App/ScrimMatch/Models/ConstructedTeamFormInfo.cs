using System.Collections.Generic;
using squittal.ScrimPlanetmans.App.Models.Planetside;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Models;

public class ConstructedTeamFormInfo : ConstructedTeamInfo
{
    public string StringId { get; set; }

    public IEnumerable<Character> Characters { get; set; }
}
