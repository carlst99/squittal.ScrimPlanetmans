using System;
using System.ComponentModel.DataAnnotations;
using squittal.ScrimPlanetmans.App.Models;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.Data.Models;

public class ScrimGrenadeAssist
{
    [Required]
    public required string ScrimMatchId { get; init; }

    [Required]
    public required DateTime Timestamp { get; init; }

    [Required]
    public required int ScrimMatchRound { get; init; }

    [Required]
    public required ScrimActionType ActionType { get; init; }

    public ulong? AttackerCharacterId { get; init; }
    public ulong? VictimCharacterId { get; init; }
    public int Points { get; init; }

    // Do not remove! Used in SQL views
    public int ExperienceGainId { get; set; }

    // Technically, different teams can have players from the same faction
    public TeamDefinition? AttackerTeamOrdinal { get; init; }
    public TeamDefinition? VictimTeamOrdinal { get; init; }
}
