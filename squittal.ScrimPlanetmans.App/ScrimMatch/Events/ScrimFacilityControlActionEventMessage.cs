using squittal.ScrimPlanetmans.App.Models;
using squittal.ScrimPlanetmans.App.Models.MessageLogs;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public class ScrimFacilityControlActionEventMessage : ScrimActionEventMessage
{
    public ScrimFacilityControlActionEvent FacilityControl { get; }

    public ScrimFacilityControlActionEventMessage(ScrimFacilityControlActionEvent facilityControl)
    {
        FacilityControl = facilityControl;

        Info = GetInfo();

        Timestamp = facilityControl.Timestamp;

        LogLevel = facilityControl.IsBanned ? ScrimMessageLogLevel.MatchEventRuleBreak : ScrimMessageLogLevel.MatchEventMajor;
    }

    private string GetInfo()
    {
        TeamDefinition teamOrdinal = FacilityControl.ControllingTeamOrdinal;

        string actionDisplay = GetEnumValueName(FacilityControl.ActionType);
        string controlTypeDisplay = FacilityControl.ControlType.ToString().ToUpper();

        string facilityName = FacilityControl.FacilityName ?? "Unknown facility";
        int facilityId = FacilityControl.FacilityId;

        string pointsDisplay = GetPointsDisplay(FacilityControl.Points);

        string bannedDisplay = FacilityControl.IsBanned ? "RULE BREAK - " : string.Empty;

        return $"{bannedDisplay}Team {teamOrdinal} {actionDisplay} {controlTypeDisplay}: {pointsDisplay} {facilityName} [{facilityId}]";
    }
}
