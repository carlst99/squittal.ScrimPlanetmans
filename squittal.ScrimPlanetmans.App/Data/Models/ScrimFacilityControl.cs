using System;
using System.ComponentModel.DataAnnotations;
using squittal.ScrimPlanetmans.App.Models;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.Data.Models;

public class ScrimFacilityControl
{
    [Required]
    public required string ScrimMatchId { get; init; }

    [Required]
    public required DateTime Timestamp { get; init; }

    [Required]
    public required int FacilityId { get; init; }

    [Required]
    public required TeamDefinition ControllingTeamOrdinal { get; init; }

    [Required]
    public required int ScrimMatchRound { get; init; }

    [Required]
    public required ScrimActionType ActionType { get; init; }

    [Required]
    public required FacilityControlType ControlType { get; init; }

    public int Points { get; init; }
}
