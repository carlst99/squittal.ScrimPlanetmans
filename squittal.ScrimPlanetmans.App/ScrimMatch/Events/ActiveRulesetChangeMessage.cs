namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public record ActiveRulesetChangeMessage
(
    Ruleset.Models.Ruleset ActiveRuleset,
    Ruleset.Models.Ruleset? PreviousActiveRuleset = null
);
