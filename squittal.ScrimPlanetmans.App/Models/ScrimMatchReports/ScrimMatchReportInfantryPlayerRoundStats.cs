namespace squittal.ScrimPlanetmans.App.Models.ScrimMatchReports;

public class ScrimMatchReportInfantryPlayerRoundStats : ScrimMatchReportStats //ScrimMatchReportInfantryPlayerStats
{
    public string ScrimMatchId { get; set; }
    public int ScrimMatchRound { get; set; }
    public string CharacterId { get; set; }
    public string NameDisplay { get; set; }
    public int FactionId { get; set; }
    public int PrestigeLevel { get; set; }
}
