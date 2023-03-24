using System.ComponentModel.DataAnnotations;
using DbgCensus.Core.Objects;
using Microsoft.EntityFrameworkCore;

namespace squittal.ScrimPlanetmans.App.Data.Models;

[Index(nameof(ScrimMatchId))]
public class ScrimMatchRoundConfiguration
{
    [Required]
    public string ScrimMatchId { get; set; }

    [Required]
    public int ScrimMatchRound { get; set; }

    public string Title { get; set; }

    public int RoundSecondsTotal { get; set; }

    public WorldDefinition WorldId { get; set; }
    public bool IsManualWorldId { get; set; } // = false;

    public uint? FacilityId { get; set; } // = -1
    public bool IsRoundEndedOnFacilityCapture { get; set; } // = false; // TODO from MatchConfiguration: move this setting to the Ruleset model

    #region Navigation Properties
    public ScrimMatch ScrimMatch { get; set; }
    #endregion Navigation Properties
}
