namespace squittal.ScrimPlanetmans.App.CensusStream.Models;

public class PlayerFacilityDefendPayload : PayloadBase
{
    public string CharacterId { get; set; }
    public int FacilityId { get; set; }
    public string OutfitId { get; set; }
}
