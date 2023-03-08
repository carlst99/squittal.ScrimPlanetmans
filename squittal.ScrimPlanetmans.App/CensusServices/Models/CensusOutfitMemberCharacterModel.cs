using squittal.ScrimPlanetmans.App.Models.CensusRest;

namespace squittal.ScrimPlanetmans.App.CensusServices.Models;

public class CensusOutfitMemberCharacterModel : CensusOutfitMemberModel
{
    public CensusCharacter.CharacterName Name { get; set; }
    //public int OnlineStatus { get; set; }
    public string OnlineStatus { get; set; }
    public int PrestigeLevel { get; set; }

    public string OutfitAlias { get; set; }
    public string OutfitAliasLower { get; set; }
}
