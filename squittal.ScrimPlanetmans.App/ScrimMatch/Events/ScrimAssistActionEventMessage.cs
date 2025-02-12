﻿using squittal.ScrimPlanetmans.App.Models.MessageLogs;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public class ScrimAssistActionEventMessage : ScrimActionEventMessage
{
    public ScrimAssistActionEvent AssistEvent { get; set; }

    public ScrimAssistActionEventMessage(ScrimAssistActionEvent assistEvent)
    {
        AssistEvent = assistEvent;
        Timestamp = assistEvent.Timestamp;

        if (assistEvent.ActionType == ScrimActionType.OutsideInterference)
        {
            LogLevel = ScrimMessageLogLevel.MatchEventWarning;

            Info = GetOutsideInterferenceInfo(assistEvent);
        }
        else
        {
            LogLevel = assistEvent.IsBanned ? ScrimMessageLogLevel.MatchEventRuleBreak : ScrimMessageLogLevel.MatchEventMinor;

            Info = GetAssistInfo(assistEvent);
        }
    }

    private string GetOutsideInterferenceInfo(ScrimAssistActionEvent assistEvent)
    {
        Player player;
        ulong? otherCharacterId;

        var actionDisplay = GetEnumValueName(assistEvent.ActionType);

        if (assistEvent.AttackerPlayer != null)
        {
            player = assistEvent.AttackerPlayer;
            otherCharacterId = assistEvent.VictimCharacterId;

            var playerName = player.NameDisplay;
            var outfitDisplay = !string.IsNullOrWhiteSpace(player.OutfitAlias)
                ? $"[{player.OutfitAlias}] "
                : string.Empty;

            return $"{actionDisplay} DMG ASSIST: {outfitDisplay}{playerName} [damaged] {otherCharacterId}";
        }

        if (assistEvent.VictimPlayer is not null)
        {
            player = assistEvent.VictimPlayer;
            otherCharacterId = assistEvent.AttackerCharacterId;

            var playerName = player.NameDisplay;
            var outfitDisplay = !string.IsNullOrWhiteSpace(player.OutfitAlias)
                ? $"[{player.OutfitAlias}] "
                : string.Empty;

            return $"{actionDisplay} DMG ASSISTED DEATH: {otherCharacterId} [damaged] {outfitDisplay}{playerName}";
        }

        return "Invalid outside interference assist (neither player is recognised)";
    }

    private string GetAssistInfo(ScrimAssistActionEvent assistEvent)
    {
        var attacker = assistEvent.AttackerPlayer;
        var victim = assistEvent.VictimPlayer;

        var attackerTeam = attacker.TeamOrdinal.ToString();

        var attackerName = attacker.NameDisplay;
        var victimName = victim.NameDisplay;

        var attackerOutfit = !string.IsNullOrWhiteSpace(attacker.OutfitAlias)
            ? $"[{attacker.OutfitAlias}] "
            : string.Empty;

        var victimOutfit = !string.IsNullOrWhiteSpace(victim.OutfitAlias)
            ? $"[{victim.OutfitAlias}] "
            : string.Empty;

        var actionDisplay = GetEnumValueName(assistEvent.ActionType);
        var pointsDisplay = GetPointsDisplay(assistEvent.Points);
        var typeDisplay = assistEvent.ActionType == ScrimActionType.UtilityAssist
            ? "utility assist"
            : "damage assist";

        var bannedDisplay = assistEvent.IsBanned ? "RULE BREAK - " : string.Empty;

        return $"{bannedDisplay}Team {attackerTeam} {actionDisplay}: {pointsDisplay} {attackerOutfit}{attackerName} [{typeDisplay}] {victimOutfit}{victimName}";
    }
}
