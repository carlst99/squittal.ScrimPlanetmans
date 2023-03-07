using squittal.ScrimPlanetmans.App.Data.Models;
using squittal.ScrimPlanetmans.App.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public record TeamConstructedTeamChangeMessage
(
    TeamDefinition TeamOrdinal,
    ConstructedTeam ConstructedTeam,
    int FactionId,
    TeamChangeType ChangeType,
    int? PlayersFound = null
)
{
    public string GetInfoMessage()
    {
        string type = ChangeType.ToString().ToUpper();
        return $"Team {TeamOrdinal} Constructed Team {type}: [{ConstructedTeam.Alias}] {ConstructedTeam.Name} [{ConstructedTeam.Id}] - Faction {FactionId}";
    }
}
