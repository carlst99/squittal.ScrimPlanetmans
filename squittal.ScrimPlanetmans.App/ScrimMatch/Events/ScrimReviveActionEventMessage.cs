﻿using squittal.ScrimPlanetmans.App.Models.MessageLogs;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public class ScrimReviveActionEventMessage : ScrimActionEventMessage
{
    public ScrimReviveActionEvent ReviveEvent { get; set; }

    public ScrimReviveActionEventMessage(ScrimReviveActionEvent reviveEvent)
    {
        ReviveEvent = reviveEvent;

        Timestamp = reviveEvent.Timestamp;

        if (reviveEvent.ActionType == ScrimActionType.OutsideInterference)
        {
            LogLevel = ScrimMessageLogLevel.MatchEventWarning;

            Info = GetOutsideInterferenceInfo(reviveEvent);
        }
        else
        {
            LogLevel = reviveEvent.IsBanned ? ScrimMessageLogLevel.MatchEventRuleBreak : ScrimMessageLogLevel.MatchEventMajor;

            Info = GetReviveInfo(reviveEvent);
        }
    }

    private string GetOutsideInterferenceInfo(ScrimReviveActionEvent reviveEvent)
    {
        var actionDisplay = GetEnumValueName(reviveEvent.ActionType);

        Player player;
        string otherCharacterId;

        if (reviveEvent.MedicPlayer != null)
        {
            player = reviveEvent.MedicPlayer;
            otherCharacterId = reviveEvent.RevivedCharacterId;

            var playerName = player.NameDisplay;
            var outfitDisplay = !string.IsNullOrWhiteSpace(player.OutfitAlias)
                ? $"[{player.OutfitAlias}] "
                : string.Empty;

            return $"{actionDisplay} REVIVE GIVEN: {outfitDisplay}{playerName} [revived] {otherCharacterId}";
        }
        else
        {
            player = reviveEvent.RevivedPlayer;
            otherCharacterId = reviveEvent.MedicCharacterId;

            var playerName = player.NameDisplay;
            var outfitDisplay = !string.IsNullOrWhiteSpace(player.OutfitAlias)
                ? $"[{player.OutfitAlias}] "
                : string.Empty;

            return $"{actionDisplay} REVIVED TAKEN: {otherCharacterId} [revived] {outfitDisplay}{playerName}";
        }
    }

    private string GetReviveInfo(ScrimReviveActionEvent reviveEvent)
    {
        var medic = reviveEvent.MedicPlayer;
        var revived = reviveEvent.RevivedPlayer;

        var medicTeam = medic.TeamOrdinal.ToString();

        var medicName = medic.NameDisplay;
        var revivedName = revived.NameDisplay;

        var medicOutfit = !string.IsNullOrWhiteSpace(medic.OutfitAlias)
            ? $"[{medic.OutfitAlias}] "
            : string.Empty;

        var revivedOutfit = !string.IsNullOrWhiteSpace(revived.OutfitAlias)
            ? $"[{revived.OutfitAlias}] "
            : string.Empty;

        var actionDisplay = GetEnumValueName(reviveEvent.ActionType);

        var bannedDisplay = reviveEvent.IsBanned ? "RULE BREAK - " : string.Empty;

        return $"{bannedDisplay}Team {medicTeam} {actionDisplay}: {medicOutfit}{medicName} [revived] {revivedOutfit}{revivedName}";
    }
}
