using System.Collections.Generic;

namespace squittal.ScrimPlanetmans.App.CensusServices.Models;

public class CensusOutfitResolveMemberCharacterModel : CensusOutfitModel
{
    public IEnumerable<CensusOutfitMemberCharacterModel> Members { get; set; }
}
