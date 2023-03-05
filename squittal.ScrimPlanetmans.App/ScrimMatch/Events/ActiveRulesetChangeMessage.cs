namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public class ActiveRulesetChangeMessage
{
    public Ruleset.Models.Ruleset ActiveRuleset { get; set; }
    public Ruleset.Models.Ruleset PreviousActiveRuleset { get; set; }

    public string Info { get; set; }

    public ActiveRulesetChangeMessage(Ruleset.Models.Ruleset activeRuleset, Ruleset.Models.Ruleset previousActiveRuleset)
    {
        ActiveRuleset = activeRuleset;
        PreviousActiveRuleset = previousActiveRuleset;

        var newNameDisplay = !string.IsNullOrWhiteSpace(ActiveRuleset.Name) ? ActiveRuleset.Name : "null";
        var oldNameDisplay = !string.IsNullOrWhiteSpace(PreviousActiveRuleset?.Name) ? PreviousActiveRuleset.Name : "null";

        Info = $"Active Ruleset Changed: {oldNameDisplay} [{PreviousActiveRuleset?.Id} => {newNameDisplay} [{ActiveRuleset.Id}]";
    }

    public ActiveRulesetChangeMessage(Ruleset.Models.Ruleset activeRuleset)
    {
        ActiveRuleset = activeRuleset;

        var newNameDisplay = !string.IsNullOrWhiteSpace(ActiveRuleset.Name) ? ActiveRuleset.Name : "null";

        Info = $"Active Ruleset Changed: none => {newNameDisplay} [{ActiveRuleset.Id}]";
    }
}
