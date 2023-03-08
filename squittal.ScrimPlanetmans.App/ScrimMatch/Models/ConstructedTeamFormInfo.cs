using System.Collections.Generic;
using squittal.ScrimPlanetmans.App.Models.CensusRest;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Models;

public class ConstructedTeamFormInfo : ConstructedTeamInfo
{
    public IEnumerable<CensusCharacter>? Characters { get; set; }
}
