using System.ComponentModel.DataAnnotations;
using DbgCensus.Core.Objects;

namespace squittal.ScrimPlanetmans.App.Data.Models;

public class ConstructedTeamPlayerMembership
{
    [Required]
    public int ConstructedTeamId { get; set; }

    [Required]
    public required ulong CharacterId { get; set; }

    public FactionDefinition FactionId { get; set; }

    public string Alias { get; set; }

    public ConstructedTeam ConstructedTeam { get; set; }
}
