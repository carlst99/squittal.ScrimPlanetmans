using System;
using System.ComponentModel.DataAnnotations;
using squittal.ScrimPlanetmans.App.Models;
using squittal.ScrimPlanetmans.App.Models.Planetside;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.Data.Models;

public class ScrimRevive
{
    [Required]
    public string ScrimMatchId { get; set; }

    [Required]
    public DateTime Timestamp { get; set; }

    [Required]
    public required ulong MedicCharacterId { get; set; }

    [Required]
    public required ulong RevivedCharacterId { get; set; }

    [Required]
    public int ScrimMatchRound { get; set; }

    public ScrimActionType ActionType { get; set; }

    // Technically, different teams can have players from the same faction
    public TeamDefinition? MedicTeamOrdinal { get; set; }
    public TeamDefinition? RevivedTeamOrdinal { get; set; }

    public int? MedicLoadoutId { get; set; }
    public int? RevivedLoadoutId { get; set; }

    public int ExperienceGainId { get; set; }
    public int ExperienceGainAmount { get; set; }

    public int? ZoneId { get; set; }
    public int WorldId { get; set; }

    public int Points { get; set; }

    #region Navigation Properties
    public ScrimMatch ScrimMatch { get; set; }
    public ScrimMatchParticipatingPlayer MedicParticipatingPlayer { get; set; }
    public ScrimMatchParticipatingPlayer RevivedParticipatingPlayer { get; set; }
    #endregion Navigation Properties
}
