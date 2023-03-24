namespace squittal.ScrimPlanetmans.App.Models.ScrimMatchReports;

public class ScrimMatchReportInfantryTeamStats : ScrimMatchReportStats
{
    public string ScrimMatchId { get; set; }
    public TeamDefinition TeamOrdinal { get; set; }
    public int PointAdjustments { get; set; }
    public int FacilityCapturePoints { get; set; }

    public int GrenadeAssists { get; set; }
    public int SpotAssists { get; set; }

    public int GrenadeTeamAssists { get; set; }
}
