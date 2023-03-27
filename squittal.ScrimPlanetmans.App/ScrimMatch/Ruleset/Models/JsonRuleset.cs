using System;
using System.Collections.Generic;
using System.Linq;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

public class JsonRuleset
{
    public string Name { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateLastModified { get; set; }

    public bool IsDefault { get; set; }

    public string FileName { get; set; }

    public string DefaultMatchTitle { get; set; }
    public int DefaultRoundLength { get; set; }
    public bool DefaultEndRoundOnFacilityCapture {get; set; }

    public ICollection<JsonRulesetActionRule>? RulesetActionRules { get; set; }
    public ICollection<JsonRulesetItemCategoryRule>? RulesetItemCategoryRules { get; set; }
    public ICollection<JsonRulesetFacilityRule>? RulesetFacilityRules { get; set; }

    public JsonRulesetOverlayConfiguration? RulesetOverlayConfiguration { get; set; }

    // Used for JSON serialization, do not remove.
    public JsonRuleset()
    {
    }

    public JsonRuleset(Ruleset ruleset, string fileName)
    {
        Name = ruleset.Name;
        DateCreated = ruleset.DateCreated;
        DateLastModified = ruleset.DateLastModified;
        IsDefault = ruleset.IsDefault;
        FileName = fileName;
        DefaultMatchTitle = ruleset.DefaultMatchTitle;
        DefaultRoundLength = ruleset.DefaultRoundLength;
        DefaultEndRoundOnFacilityCapture = ruleset.DefaultEndRoundOnFacilityCapture;

        if (ruleset.RulesetActionRules?.Any() is true)
        {
            RulesetActionRules = ruleset.RulesetActionRules.Select(r => new JsonRulesetActionRule(r)).ToArray();
        }

        if (ruleset.RulesetItemCategoryRules?.Any() is true)
        {
            RulesetItemCategoryRules = ruleset.RulesetItemCategoryRules.Select
                (
                    r => new JsonRulesetItemCategoryRule
                        (
                            r,
                            GetItemCategoryJsonItemRules(ruleset.RulesetItemRules, r.ItemCategoryId)
                        )
                )
                .ToArray();
        }

        if (ruleset.RulesetFacilityRules?.Any() is true)
        {
            RulesetFacilityRules = ruleset.RulesetFacilityRules.Select(r => new JsonRulesetFacilityRule(r)).ToArray();
        }

        if (ruleset.RulesetOverlayConfiguration != null)
        {
            RulesetOverlayConfiguration = new JsonRulesetOverlayConfiguration(ruleset.RulesetOverlayConfiguration);
        }
    }

    private ICollection<JsonRulesetItemRule> GetItemCategoryJsonItemRules
    (
        ICollection<RulesetItemRule>? allItemRules,
        uint itemCategoryId
    )
    {
        if (allItemRules is null || allItemRules.All(r => r.ItemCategoryId != itemCategoryId))
            return Array.Empty<JsonRulesetItemRule>();

        return allItemRules.Where(r => r.ItemCategoryId == itemCategoryId).Select(r => new JsonRulesetItemRule(r)).ToArray();
    }
}
