using System;
using System.Diagnostics.CodeAnalysis;
using DbgCensus.Core.Objects;
using squittal.ScrimPlanetmans.App.Models;
using squittal.ScrimPlanetmans.App.Models.CensusRest;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Models;

public abstract class ScrimActionEvent
{
    public required int ZoneId { get; init; }
    public required DateTime Timestamp { get; init; }

    public ScrimActionType ActionType { get; set; }
    public bool IsBanned { get; set; }
}

public class ScrimDeathActionEvent : ScrimActionEvent
{
    /// <summary>
    /// May be null in cases such as outside interference.
    /// </summary>
    public Player? AttackerPlayer { get; init; }
    public required Player VictimPlayer { get; init; }
    public required ScrimActionWeaponInfo Weapon { get; init; }
    public required ulong AttackerCharacterId { get; init; }
    public required ulong VictimCharacterId { get; init; }
    public required uint AttackerLoadoutId { get; init; }
    public required uint VictimLoadoutId { get; init; }
    public int? AttackerVehicleId { get; set; }
    public bool IsHeadshot { get; init; }

    public int Points { get; set; }
    public DeathEventType DeathType { get; set; }
}

public class ScrimExperienceGainActionEvent : ScrimActionEvent
{
    public required ScrimActionExperienceGainInfo ExperienceGainInfo { get; set; }

    public ExperienceType ExperienceType { get; set; }

    public uint LoadoutId { get; set; }

    public int Points { get; set; }
}

public class ScrimReviveActionEvent : ScrimExperienceGainActionEvent
{
    public Player? MedicPlayer { get; set; }
    public Player? RevivedPlayer { get; set; }

    public ulong MedicCharacterId { get; }
    public ulong RevivedCharacterId { get; }

    [SetsRequiredMembers]
    public ScrimReviveActionEvent
    (
        ScrimExperienceGainActionEvent baseExperienceEvent,
        ulong medicCharacterId,
        ulong revivedCharacterId
    )
    {
        Timestamp = baseExperienceEvent.Timestamp;
        ZoneId = baseExperienceEvent.ZoneId;
        ExperienceGainInfo = baseExperienceEvent.ExperienceGainInfo;
        ExperienceType = baseExperienceEvent.ExperienceType;
        LoadoutId = baseExperienceEvent.LoadoutId;
        MedicCharacterId = medicCharacterId;
        RevivedCharacterId = revivedCharacterId;
    }
}

public class ScrimAssistActionEvent : ScrimExperienceGainActionEvent
{
    public Player? AttackerPlayer { get; }
    public Player? VictimPlayer { get; }

    public ulong AttackerCharacterId { get; }
    public ulong? VictimCharacterId { get; }

    [SetsRequiredMembers]
    public ScrimAssistActionEvent
    (
        ScrimExperienceGainActionEvent baseExperienceEvent,
        ulong attackerCharacterId,
        Player? attackerPlayer,
        ulong? victimCharacterId,
        Player? victimPlayer
    )
    {
        Timestamp = baseExperienceEvent.Timestamp;
        ZoneId = baseExperienceEvent.ZoneId;
        ExperienceGainInfo = baseExperienceEvent.ExperienceGainInfo;
        ExperienceType = baseExperienceEvent.ExperienceType;
        LoadoutId = baseExperienceEvent.LoadoutId;
        AttackerCharacterId = attackerCharacterId;
        AttackerPlayer = attackerPlayer;
        VictimCharacterId = victimCharacterId;
        VictimPlayer = victimPlayer;
    }
}

public class ScrimUtilityAssistActionEvent : ScrimExperienceGainActionEvent
{
    public Player AttackerPlayer { get; set; }
    public Player VictimPlayer { get; set; }

    public string AttackerCharacterId { get; set; }
    public string VictimCharacterId { get; set; }

    public ScrimUtilityAssistActionEvent(ScrimExperienceGainActionEvent baseExperienceEvent)
    {
        Timestamp = baseExperienceEvent.Timestamp;
        ZoneId = baseExperienceEvent.ZoneId;
        ExperienceGainInfo = baseExperienceEvent.ExperienceGainInfo;
        ExperienceType = baseExperienceEvent.ExperienceType;
        LoadoutId = baseExperienceEvent.LoadoutId;
    }
}

public class ScrimObjectiveTickActionEvent : ScrimExperienceGainActionEvent
{
    public Player? Player { get; set; }

    public ulong PlayerCharacterId { get; }

    // TODO: Experience IDs of 15 & 16 (Control Point - Attack / Defend seem to populate other_id with
    // an opposing player, but not sure what it means at the moment
    // public Player OtherPlayer { get; set; }

    //public ScrimActionExperienceGainInfo ExperienceGain { get; set; }

    //public int? LoadoutId { get; set; }

    //public int Points { get; set; }

    [SetsRequiredMembers]
    public ScrimObjectiveTickActionEvent(ScrimExperienceGainActionEvent baseExperienceEvent, ulong playerCharacterId)
    {
        Timestamp = baseExperienceEvent.Timestamp;
        ZoneId = baseExperienceEvent.ZoneId;
        ExperienceGainInfo = baseExperienceEvent.ExperienceGainInfo;
        ExperienceType = baseExperienceEvent.ExperienceType;
        LoadoutId = baseExperienceEvent.LoadoutId;
        PlayerCharacterId = playerCharacterId;
    }
}

public class ScrimLoginActionEvent : ScrimActionEvent
{
    public Player Player { get; set; }

    public ScrimLoginActionEvent()
    {
        ActionType = ScrimActionType.Login;
    }
}

public class ScrimLogoutActionEvent : ScrimActionEvent
{
    public Player Player { get; set; }

    public ScrimLogoutActionEvent()
    {
        ActionType = ScrimActionType.Logout;
    }
}

public class ScrimFacilityControlActionEvent : ScrimActionEvent
{
    public int FacilityId { get; set; }
    public WorldDefinition WorldId { get; set; }
    public string? FacilityName { get; set; }

    public FacilityControlType ControlType { get; set; }
    public TeamDefinition ControllingTeamOrdinal { get; set; }

    public int Points { get; set; }


    public int? NewFactionId { get; set; }
    public int? OldFactionId { get; set; }
    public int DurationHeld { get; set; }
    public string OutfitId { get; set; }
}

public record ScrimActionWeaponInfo
(
    uint Id,
    uint? ItemCategoryId,
    string? Name,
    bool? IsVehicleWeapon
);

public record ScrimActionExperienceGainInfo(int Id, int Amount);

public class ScrimVehicleDestructionActionEvent : ScrimActionEvent
{
    public required ScrimActionWeaponInfo Weapon { get; init; }
    public required ulong VictimCharacterId { get; init; }

    public ulong AttackerCharacterId { get; set; }
    public Player? AttackerPlayer { get; set; }
    public Player? VictimPlayer { get; set; }
    public ScrimActionVehicleInfo? AttackerVehicle { get; set; }
    public ScrimActionVehicleInfo? VictimVehicle { get; set; }
    public uint AttackerLoadoutId { get; set; }
    public int Points { get; set; }
    public DeathEventType DeathType { get; set; }
}

public class ScrimActionVehicleInfo
{
    public uint Id { get; set; }
    public string Name { get; set; }

    public VehicleType Type { get; set; }

    public ScrimActionVehicleInfo(CensusVehicle? vehicle)
    {
        if (vehicle is null)
            return;

        Id = vehicle.VehicleId;
        Name = vehicle.Name.English.GetValueOrDefault() ?? "Unknown Vehicle";

        Type = GetVehicleType(vehicle.VehicleId);
    }

    public static VehicleType GetVehicleType(uint vehicleId)
        => vehicleId switch
        {
            1 or 2010 or 2125 => VehicleType.Flash,
            2 => VehicleType.Sunderer,
            3 => VehicleType.Lightning,
            4 or 5 or 6 => VehicleType.MBT,
            7 or 8 or 9 => VehicleType.ESF,
            10 => VehicleType.Liberator,
            11 => VehicleType.Galaxy,
            12 => VehicleType.Harasser,
            14 => VehicleType.Valkyrie,
            15 => VehicleType.ANT,
            2019 => VehicleType.Bastion,
            2122 => VehicleType.Interceptor,
            2123 => VehicleType.Interceptor,
            2124 => VehicleType.Interceptor,
            2142 => VehicleType.Corsair,
            _ => VehicleType.Unknown
        };
}
