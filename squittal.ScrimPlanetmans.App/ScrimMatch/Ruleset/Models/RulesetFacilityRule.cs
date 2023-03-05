using System.ComponentModel.DataAnnotations;
using squittal.ScrimPlanetmans.App.Models.Planetside;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

public class RulesetFacilityRule
{
    [Required]
    public int RulesetId { get; set; }
    [Required]
    public int FacilityId { get; set; }
    [Required]
    public int MapRegionId { get; set; }

    public Ruleset Ruleset { get; set; }
    public MapRegion MapRegion { get; set; }
}
