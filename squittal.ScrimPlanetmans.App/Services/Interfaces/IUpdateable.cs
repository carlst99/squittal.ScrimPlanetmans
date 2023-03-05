using System.Threading.Tasks;

namespace squittal.ScrimPlanetmans.App.Services.Interfaces;

public interface IUpdateable
{
    Task RefreshStore(bool onlyQueryCensusIfEmpty = false, bool canUseBackupScript = false);
}
