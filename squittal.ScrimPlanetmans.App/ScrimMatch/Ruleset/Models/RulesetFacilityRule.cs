using System.ComponentModel.DataAnnotations;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

public class RulesetFacilityRule
{
    [Required]
    public int RulesetId { get; set; }

    [Required]
    public required uint FacilityId { get; init; }

    public Ruleset Ruleset { get; set; }
}
