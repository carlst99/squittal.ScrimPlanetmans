using squittal.ScrimPlanetmans.App.Models.CensusRest;

namespace squittal.ScrimPlanetmans.App.CensusServices.Models;

public class CensusOutfitMemberCharacterNameModel : CensusOutfitMemberModel
{
    public CensusCharacter.CharacterName Name { get; set; }
}
