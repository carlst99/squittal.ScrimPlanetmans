using System.Collections.Generic;
using System.Text;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public class RulesetSettingChangeMessage
{
    public Ruleset.Models.Ruleset Ruleset { get; }
    public List<RulesetSettingChange> ChangedSettings { get; }
    public string Info => GetInfoString();

    public RulesetSettingChangeMessage(Ruleset.Models.Ruleset ruleset, Ruleset.Models.Ruleset previousRuleset)
    {
        Ruleset = ruleset;
        ChangedSettings = new List<RulesetSettingChange>();

        CalculateSettingChanges(ruleset, previousRuleset);
    }

    private void CalculateSettingChanges(Ruleset.Models.Ruleset ruleset, Ruleset.Models.Ruleset previousRuleset)
    {
        if (ruleset.Name != previousRuleset.Name)
        {
            ChangedSettings.Add(RulesetSettingChange.Name);
        }

        if (ruleset.DefaultMatchTitle != previousRuleset.DefaultMatchTitle)
        {
            ChangedSettings.Add(RulesetSettingChange.DefaultMatchTitle);
        }

        if (ruleset.DefaultRoundLength != previousRuleset.DefaultRoundLength)
        {
            ChangedSettings.Add(RulesetSettingChange.DefaultRoundLength);
        }

        if (ruleset.DefaultEndRoundOnFacilityCapture != previousRuleset.DefaultEndRoundOnFacilityCapture)
        {
            ChangedSettings.Add(RulesetSettingChange.DefaultEndRoundOnFacilityCapture);
        }
    }

    private string GetInfoString()
    {
        StringBuilder stringBuilder = new($"Settings changed for Ruleset {Ruleset.Name} [{Ruleset.Id}]: ");

        if (ChangedSettings.Count is 0)
        {
            stringBuilder.Append("none");
            return stringBuilder.ToString();
        }

        bool isFirst = true;
        foreach (RulesetSettingChange setting in ChangedSettings)
        {
            if (!isFirst)
                stringBuilder.Append(", ");

            stringBuilder.Append(setting);
            isFirst = false;
        }

        return stringBuilder.ToString();
    }
}

public enum RulesetSettingChange
{
    Name,
    DefaultMatchTitle,
    DefaultRoundLength,
    DefaultEndRoundOnFacilityCapture
}
