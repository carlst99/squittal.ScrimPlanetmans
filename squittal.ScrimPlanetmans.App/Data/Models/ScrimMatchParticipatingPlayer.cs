using System.ComponentModel.DataAnnotations;
using DbgCensus.Core.Objects;
using squittal.ScrimPlanetmans.App.Models;

namespace squittal.ScrimPlanetmans.App.Data.Models;

public class ScrimMatchParticipatingPlayer
{
    [Required]
    public required string ScrimMatchId { get; init; }

    [Required]
    public required ulong CharacterId { get; init; }

    [Required]
    public required TeamDefinition TeamOrdinal { get; init; }

    // Do not remove! Used in SQL views
    [Required]
    public required string NameDisplay { get; init; }

    // Do not remove! Used in SQL views
    [Required]
    public required string NameFull { get; init; }

    public FactionDefinition FactionId { get; init; }

    // Do not remove! Used in SQL views
    public int PrestigeLevel { get; init; }
    public bool IsFromOutfit { get; init; }
    public ulong? OutfitId { get; init; }
    public bool IsFromConstructedTeam { get; init; }
    public int? ConstructedTeamId { get; init; }
}
