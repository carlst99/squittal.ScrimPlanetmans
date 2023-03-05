using System;
using squittal.ScrimPlanetmans.App.Models.MessageLogs;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public class ScrimFacilityControlActionEventMessage : ScrimActionEventMessage
{
    public ScrimFacilityControlActionEvent FacilityControl { get; set; }

    public ScrimFacilityControlActionEventMessage(ScrimFacilityControlActionEvent facilityControl)
    {
        FacilityControl = facilityControl;

        Info = GetInfo();

        Timestamp = facilityControl.Timestamp;

        LogLevel = facilityControl.IsBanned ? ScrimMessageLogLevel.MatchEventRuleBreak : ScrimMessageLogLevel.MatchEventMajor;
    }

    private string GetInfo()
    {
        var teamOrdinal = FacilityControl.ControllingTeamOrdinal;

        var actionDisplay = GetEnumValueName(FacilityControl.ActionType);
        var controlTypeDisplay = Enum.GetName(typeof(FacilityControlType), FacilityControl.ControlType).ToUpper();

        var facilityName = FacilityControl.FacilityName;
        var facilityId = FacilityControl.FacilityId;

        var pointsDisplay = GetPointsDisplay(FacilityControl.Points);

        var bannedDisplay = FacilityControl.IsBanned ? "RULE BREAK - " : string.Empty;

        return $"{bannedDisplay}Team {teamOrdinal} {actionDisplay} {controlTypeDisplay}: {pointsDisplay} {facilityName} [{facilityId}]";
    }
}
