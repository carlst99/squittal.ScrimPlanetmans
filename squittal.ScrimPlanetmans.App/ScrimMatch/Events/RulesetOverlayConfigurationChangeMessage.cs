using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public class RulesetOverlayConfigurationChangeMessage
{
    public Ruleset.Models.Ruleset Ruleset { get; set; }
    public RulesetOverlayConfiguration OverlayConfiguration { get; set; }
    public List<RulesetOverlayConfigurationChange> ChangedSettings { get; set; } = new List<RulesetOverlayConfigurationChange>();
    public string Info => GetInfoString();

    public RulesetOverlayConfigurationChangeMessage(Ruleset.Models.Ruleset ruleset, RulesetOverlayConfiguration newConfiguration, RulesetOverlayConfiguration previousConfiguration)
    {
        Ruleset = ruleset;
        OverlayConfiguration = newConfiguration;

        CalculateSettingChanges(newConfiguration, previousConfiguration);
    }

    private void CalculateSettingChanges(RulesetOverlayConfiguration newConfiguration, RulesetOverlayConfiguration previousConfiguration)
    {
        if (newConfiguration.UseCompactLayout != previousConfiguration?.UseCompactLayout)
        {
            ChangedSettings.Add(RulesetOverlayConfigurationChange.UseCompactLayout);
        }

        if (newConfiguration.StatsDisplayType != previousConfiguration?.StatsDisplayType)
        {
            ChangedSettings.Add(RulesetOverlayConfigurationChange.StatsDisplayType);
        }

        if (newConfiguration.ShowStatusPanelScores != previousConfiguration?.ShowStatusPanelScores)
        {
            ChangedSettings.Add(RulesetOverlayConfigurationChange.ShowStatusPanelScores);
        }
    }

    private string GetInfoString()
    {
        if (Ruleset == null)
        {
            return string.Empty;
        }

        var stringBuilder = new StringBuilder($"Overlay Configuration changed for Ruleset {Ruleset.Name} [{Ruleset.Id}]: ");

        if (ChangedSettings == null || !ChangedSettings.Any())
        {
            stringBuilder.Append("none");

            return stringBuilder.ToString();
        }

        var isFirst = true;
        foreach (var setting in ChangedSettings)
        {
            var settingString = isFirst ? GetEnumValueName(setting) : $", {GetEnumValueName(setting)}";

            stringBuilder.Append(settingString);

            isFirst = false;
        }

        return stringBuilder.ToString();
    }

    private string GetEnumValueName(RulesetOverlayConfigurationChange type)
    {
        return Enum.GetName(typeof(RulesetOverlayConfigurationChange), type);
    }
}

public enum RulesetOverlayConfigurationChange
{
    UseCompactLayout,
    StatsDisplayType,
    ShowStatusPanelScores
}
