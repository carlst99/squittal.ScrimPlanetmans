using DbgCensus.Core.Objects;

namespace squittal.ScrimPlanetmans.App.Models.Forms;

public class ConstructedTeamFactionSelection
{
    public string ConstructedTeamStringId { get; set; } = string.Empty;

    public int ConstructedTeamId
    {
        get
        {
            if (int.TryParse(ConstructedTeamStringId, out int intId))
            {
                return intId;
            }
            else
            {
                return -1;
            }
        }
    }

    public FactionDefinition FactionId { get; set; } = FactionDefinition.VS;
}
