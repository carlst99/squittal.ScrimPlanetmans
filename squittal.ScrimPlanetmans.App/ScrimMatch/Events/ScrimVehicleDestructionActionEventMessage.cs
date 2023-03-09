using squittal.ScrimPlanetmans.App.Models.MessageLogs;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public class ScrimVehicleDestructionActionEventMessage : ScrimActionEventMessage
{
    public ScrimVehicleDestructionActionEvent DestructionEvent { get; set; }

    public ScrimVehicleDestructionActionEventMessage(ScrimVehicleDestructionActionEvent destructionEvent)
    {
        DestructionEvent = destructionEvent;

        Timestamp = destructionEvent.Timestamp;

        if (DestructionEvent.ActionType == ScrimActionType.OutsideInterference)
        {
            LogLevel = ScrimMessageLogLevel.MatchEventWarning;

            Info = GetOutsideInterferenceInfo(DestructionEvent);
        }
        else
        {
            LogLevel = destructionEvent.IsBanned ? ScrimMessageLogLevel.MatchEventRuleBreak : ScrimMessageLogLevel.MatchEventMajor;

            switch (DestructionEvent.DeathType)
            {
                case DeathEventType.Kill:
                    Info = GetKillInfo(DestructionEvent);
                    break;

                case DeathEventType.Teamkill:
                    Info = GetTeamkillInfo(DestructionEvent);
                    break;

                case DeathEventType.Suicide:
                    Info = GetSuicideInfo(DestructionEvent);
                    break;
            }
        }
    }

    private string GetOutsideInterferenceInfo(ScrimVehicleDestructionActionEvent destructionEvent)
    {
        Player? player;
        ulong otherCharacterId;

        string? weaponName = destructionEvent.Weapon.Name;
        string victimVehicleName = destructionEvent.VictimVehicle != null ? destructionEvent.VictimVehicle.Name : "Unknown vehicle";
        string actionDisplay = GetEnumValueName(destructionEvent.ActionType);

        if (destructionEvent.AttackerPlayer != null)
        {
            player = destructionEvent.AttackerPlayer;
            otherCharacterId = destructionEvent.VictimCharacterId;

            string playerName = player.NameDisplay;
            string outfitDisplay = !string.IsNullOrWhiteSpace(player.OutfitAlias)
                ? $"[{player.OutfitAlias}] "
                : string.Empty;

            return $"{actionDisplay} VEHICLE DESTROYED: {outfitDisplay}{playerName} {{{weaponName}}} {victimVehicleName} ({otherCharacterId})";
        }

        if (destructionEvent.VictimPlayer is not null)
        {
            player = destructionEvent.VictimPlayer;
            otherCharacterId = destructionEvent.AttackerCharacterId;

            string playerName = player.NameDisplay;
            string outfitDisplay = !string.IsNullOrWhiteSpace(player.OutfitAlias)
                ? $"[{player.OutfitAlias}] "
                : string.Empty;

            return $"{actionDisplay} VEHICLE LOST: {otherCharacterId} {{{weaponName}}} {victimVehicleName} ({outfitDisplay}{playerName})";
        }

        return "Invalid outside interference vehicle destroy (neither player is recognised)";
    }

    private string GetKillInfo(ScrimVehicleDestructionActionEvent destructionEvent)
    {
        Player? attacker = destructionEvent.AttackerPlayer;
        Player? victim = destructionEvent.VictimPlayer;

        if (attacker is null)
            return "Invalid kill: no attacker";

        string attackerTeam = attacker.TeamOrdinal.ToString();

        string attackerName = attacker.NameDisplay;

        string victimName = victim is not null ? victim.NameDisplay : string.Empty;

        string attackerOutfit = !string.IsNullOrWhiteSpace(attacker.OutfitAlias)
            ? $"[{attacker.OutfitAlias}] "
            : string.Empty;


        string victimOutfit = !string.IsNullOrWhiteSpace(victim?.OutfitAlias)
            ? $"[{victim.OutfitAlias}] "
            : string.Empty;

        string actionDisplay = GetEnumValueName(destructionEvent.ActionType);
        string pointsDisplay = GetPointsDisplay(destructionEvent.Points);

        string? weaponName = DestructionEvent.Weapon.Name;
        string victimVehicleName = DestructionEvent.VictimVehicle != null ? DestructionEvent.VictimVehicle.Name : "Unknown vehicle";

        string bannedDisplay = destructionEvent.IsBanned ? "RULE BREAK - " : string.Empty;

        return $"{bannedDisplay}Team {attackerTeam} {actionDisplay}: {pointsDisplay} {attackerOutfit}{attackerName} {{{weaponName}}} {victimVehicleName} ({victimOutfit}{victimName})";
    }

    private string GetTeamkillInfo(ScrimVehicleDestructionActionEvent destructionEvent)
    {
        return GetKillInfo(destructionEvent);
    }

    private string GetSuicideInfo(ScrimVehicleDestructionActionEvent destructionEvent)
    {
        Player? attacker = destructionEvent.AttackerPlayer;

        string attackerTeam = attacker.TeamOrdinal.ToString();

        string attackerName = attacker.NameDisplay;

        string attackerOutfit = !string.IsNullOrWhiteSpace(attacker.OutfitAlias)
            ? $"[{attacker.OutfitAlias}] "
            : string.Empty;

        string actionDisplay = GetEnumValueName(destructionEvent.ActionType);
        string pointsDisplay = GetPointsDisplay(destructionEvent.Points);

        string? weaponName = DestructionEvent.Weapon.Name;
        string victimVehicleName = DestructionEvent.VictimVehicle != null ? DestructionEvent.VictimVehicle.Name : "Unknown vehicle";

        string bannedDisplay = destructionEvent.IsBanned ? "RULE BREAK - " : string.Empty;

        return $"{bannedDisplay}Team {attackerTeam} {actionDisplay}: {pointsDisplay} {attackerOutfit}{attackerName} ({victimVehicleName}) {{{weaponName}}}";
    }
}
