using squittal.ScrimPlanetmans.App.ScrimMatch.Events;

namespace squittal.ScrimPlanetmans.App.Models.Forms;

public class ConstructedTeamCharacterChange
{
    public string CharacterInput { get; set; }

    public TeamPlayerChangeType ChangeType { get; set; }
}
