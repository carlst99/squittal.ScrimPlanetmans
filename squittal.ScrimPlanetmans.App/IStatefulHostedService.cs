// Credit to Lampjaw

using System.Threading;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models;

namespace squittal.ScrimPlanetmans.App;

public interface IStatefulHostedService
{
    Task OnApplicationStartup(CancellationToken cancellationToken);
    Task OnApplicationShutdown(CancellationToken cancellationToken);
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    Task<ServiceState> GetStateAsync(CancellationToken cancellationToken);
    string ServiceName { get; }
}
