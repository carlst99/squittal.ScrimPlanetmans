namespace squittal.ScrimPlanetmans.App.ScrimMatch.Models;

public class ConstructedTeamInfo
{
    public required int? Id { get; init; }
    public required string Name { get; set; }
    public required string Alias { get; set; }
    public bool IsHiddenFromSelection { get; set; }
}
