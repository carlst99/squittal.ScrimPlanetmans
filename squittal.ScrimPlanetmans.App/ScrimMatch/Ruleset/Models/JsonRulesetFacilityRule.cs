namespace squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

public class JsonRulesetFacilityRule
{
    public uint FacilityId { get; set; }
    public uint MapRegionId { get; set; }

    public JsonRulesetFacilityRule(RulesetFacilityRule rule)
    {
        FacilityId = rule.FacilityId;
        MapRegionId = rule.MapRegionId;
    }
}
