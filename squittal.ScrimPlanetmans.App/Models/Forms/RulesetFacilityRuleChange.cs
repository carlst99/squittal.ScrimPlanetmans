﻿using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.Models.Forms;

public class RulesetFacilityRuleChange
{
    public RulesetFacilityRule RulesetFacilityRule { get; set; }

    public RulesetFacilityRuleChangeType ChangeType { get; set; }

    public RulesetFacilityRuleChange(RulesetFacilityRule rule, RulesetFacilityRuleChangeType changeType)
    {
        RulesetFacilityRule = rule;
        ChangeType = changeType;
    }
}

public enum RulesetFacilityRuleChangeType
{
    Add,
    Remove,
    Reset
}
