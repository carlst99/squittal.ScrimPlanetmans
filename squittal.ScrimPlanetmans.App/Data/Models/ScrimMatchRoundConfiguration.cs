using System.ComponentModel.DataAnnotations;
using DbgCensus.Core.Objects;

namespace squittal.ScrimPlanetmans.App.Data.Models;

public class ScrimMatchRoundConfiguration
{
    [Required]
    public string ScrimMatchId { get; set; }

    [Required]
    public int ScrimMatchRound { get; set; }

    public string Title { get; set; }

    public int RoundSecondsTotal { get; set; }

    public WorldDefinition WorldId { get; set; }
    public bool IsManualWorldId { get; set; }

    public uint? FacilityId { get; set; }
    public bool IsRoundEndedOnFacilityCapture { get; set; } // TODO from MatchConfiguration: move this setting to the Ruleset model

    #region Navigation Properties
    public ScrimMatch ScrimMatch { get; set; }
    #endregion Navigation Properties
}
