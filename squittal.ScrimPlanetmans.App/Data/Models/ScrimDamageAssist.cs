﻿using System;
using System.ComponentModel.DataAnnotations;
using squittal.ScrimPlanetmans.App.Models;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.Data.Models;

public class ScrimDamageAssist
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
    public TeamDefinition? AttackerTeamOrdinal { get; init; }
    public TeamDefinition? VictimTeamOrdinal { get; init; }
    public int Points { get; init; }
}
