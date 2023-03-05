using System;
using System.ComponentModel.DataAnnotations;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.Data.Models;

public class ScrimMatchTeamPointAdjustment
{
    [Required]
    public string ScrimMatchId { get; set; }
    [Required]
    public int TeamOrdinal { get; set; }
    [Required]
    public DateTime Timestamp { get; set;}

    public int Points { get; set; }

    public PointAdjustmentType AdjustmentType { get; set; }

    public string Rationale { get; set; }

    public ScrimMatchTeamResult ScrimMatchTeamResult { get; set; }
}
