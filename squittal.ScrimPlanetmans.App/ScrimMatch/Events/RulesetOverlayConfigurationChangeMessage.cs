using System.Collections.Generic;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public class RulesetOverlayConfigurationChangeMessage
{
    public Ruleset.Models.Ruleset Ruleset { get; }
    public RulesetOverlayConfiguration OverlayConfiguration { get; }
    public List<RulesetOverlayConfigurationChange> ChangedSettings { get; }

    public RulesetOverlayConfigurationChangeMessage
    (
        Ruleset.Models.Ruleset ruleset,
        RulesetOverlayConfiguration newConfiguration,
        RulesetOverlayConfiguration? previousConfiguration
    )
    {
        Ruleset = ruleset;
        OverlayConfiguration = newConfiguration;
        ChangedSettings = new List<RulesetOverlayConfigurationChange>();

        CalculateSettingChanges(newConfiguration, previousConfiguration);
    }

    private void CalculateSettingChanges
    (
        RulesetOverlayConfiguration newConfiguration,
        RulesetOverlayConfiguration? previousConfiguration
    )
    {
        if (newConfiguration.UseCompactLayout != previousConfiguration?.UseCompactLayout)
            ChangedSettings.Add(RulesetOverlayConfigurationChange.UseCompactLayout);

        if (newConfiguration.StatsDisplayType != previousConfiguration?.StatsDisplayType)
            ChangedSettings.Add(RulesetOverlayConfigurationChange.StatsDisplayType);

        if (newConfiguration.ShowStatusPanelScores != previousConfiguration?.ShowStatusPanelScores)
            ChangedSettings.Add(RulesetOverlayConfigurationChange.ShowStatusPanelScores);
    }
}

public enum RulesetOverlayConfigurationChange
{
    UseCompactLayout,
    StatsDisplayType,
    ShowStatusPanelScores
}
