namespace squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

public class JsonRulesetFacilityRule
{
    public uint FacilityId { get; set; }

    // Used for JSON serialization, do not remove.
    public JsonRulesetFacilityRule()
    {
    }

    public JsonRulesetFacilityRule(RulesetFacilityRule rule)
    {
        FacilityId = rule.FacilityId;
    }
}
