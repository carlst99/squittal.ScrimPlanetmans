using squittal.ScrimPlanetmans.App.Models.MessageLogs;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public class ScrimObjectiveTickActionEventMessage : ScrimActionEventMessage
{
    public ScrimObjectiveTickActionEvent ObjectiveTickEvent { get; set; }

    public ScrimObjectiveTickActionEventMessage(ScrimObjectiveTickActionEvent objectiveTickEvent)
    {
        ObjectiveTickEvent = objectiveTickEvent;

        Timestamp = objectiveTickEvent.Timestamp;

        LogLevel = objectiveTickEvent.IsBanned ? ScrimMessageLogLevel.MatchEventRuleBreak : ScrimMessageLogLevel.MatchEventMinor;

        Info = GetInfo(objectiveTickEvent);
    }

    private string GetInfo(ScrimObjectiveTickActionEvent objectiveTickEvent)
    {
        var attacker = objectiveTickEvent.Player;

        var attackerTeam = attacker.TeamOrdinal.ToString();

        var attackerName = attacker.NameDisplay;

        var attackerOutfit = !string.IsNullOrWhiteSpace(attacker.OutfitAlias)
            ? $"[{attacker.OutfitAlias}] "
            : string.Empty;

        var actionDisplay = GetEnumValueName(objectiveTickEvent.ActionType);
        var pointsDisplay = GetPointsDisplay(objectiveTickEvent.Points);

        var bannedDisplay = objectiveTickEvent.IsBanned ? "RULE BREAK - " : string.Empty;

        return $"{bannedDisplay}Team {attackerTeam} {actionDisplay}: {pointsDisplay} {attackerOutfit}{attackerName}";
    }
}
