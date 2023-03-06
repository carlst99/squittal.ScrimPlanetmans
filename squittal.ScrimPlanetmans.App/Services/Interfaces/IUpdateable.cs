using System.Threading;
using System.Threading.Tasks;

namespace squittal.ScrimPlanetmans.App.Services.Interfaces;

public interface IUpdateable
{
    Task RefreshStoreAsync
    (
        bool onlyQueryCensusIfEmpty = false,
        bool canUseBackupScript = false,
        CancellationToken ct = default
    );
}
