using System;
using System.ComponentModel.DataAnnotations;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.Data.Models;

public class ScrimVehicleDestruction
{
    [Required]
    public required string ScrimMatchId { get; init; }

    [Required]
    public required DateTime Timestamp { get; init; }

    [Required]
    public required ulong AttackerCharacterId { get; init; }

    [Required]
    public required ulong VictimCharacterId { get; init; }

    [Required]
    public required uint VictimVehicleId { get; init; }

    public uint? AttackerVehicleId { get; init; }
    public int ScrimMatchRound { get; init; }
    public ScrimActionType ActionType { get; init; }
    public DeathEventType DeathType { get; init; }

    public uint? WeaponId { get; init; }

    public int Points { get; init; }
}
