using System;
using System.ComponentModel.DataAnnotations;
using DbgCensus.Core.Objects;
using squittal.ScrimPlanetmans.App.Models;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.Data.Models;

public class ScrimDeath
{
    [Required]
    public required string ScrimMatchId { get; init; }

    [Required]
    public required DateTime Timestamp { get; init; }

    [Required]
    public required ulong VictimCharacterId { get; init; }

    [Required]
    public required int ScrimMatchRound { get; init; }

    [Required]
    public required ScrimActionType ActionType { get; init; }

    public ulong? AttackerCharacterId { get; init; }

    // Do not remove! Used in SQL views
    public FactionDefinition? AttackerFactionId { get; init; }

    // Do not remove! Used in SQL views
    public uint AttackerLoadoutId { get; init; }
    public DeathEventType DeathType { get; init; }
    public TeamDefinition? AttackerTeamOrdinal { get; init; }
    public TeamDefinition? VictimTeamOrdinal { get; init; }
    public bool IsHeadshot { get; init; }
    public int Points { get; init; }
    public uint? WeaponId { get; init; }
    public int? AttackerVehicleId { get; init; }
}
