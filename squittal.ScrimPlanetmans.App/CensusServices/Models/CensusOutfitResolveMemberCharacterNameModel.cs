using System.Collections.Generic;

namespace squittal.ScrimPlanetmans.App.CensusServices.Models;

public class CensusOutfitResolveMemberCharacterNameModel : CensusOutfitModel
{
    public IEnumerable<CensusOutfitMemberCharacterNameModel> Members { get; set; }
}
