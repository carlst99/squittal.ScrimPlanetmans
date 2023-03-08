namespace squittal.ScrimPlanetmans.App.Models;

public class ConstructedTeamMemberDetails
{
    public required ulong CharacterId { get; init; }
    public required int ConstructedTeamId { get; init; }
    public required int FactionId { get; init; }
    public required string NameFull { get; init; }
    public string? NameAlias { get; set; }
    public int WorldId { get; init; }
    public int PrestigeLevel { get; init; }
    public bool IsMatchParticipant { get; set; }

    public bool IsDeleteAllowed => !IsMatchParticipant;

    public string NameDisplay => string.IsNullOrWhiteSpace(NameAlias)
        ? NameFull
        : NameAlias;
}
