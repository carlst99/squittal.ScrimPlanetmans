using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using squittal.ScrimPlanetmans.App.CensusStream.Interfaces;

namespace squittal.ScrimPlanetmans.App.CensusStream;

public class WebsocketMonitorHostedService : IHostedService
{
    private readonly IWebsocketMonitor _service;

    public WebsocketMonitorHostedService(IWebsocketMonitor service)
    {
        _service = service;
    }
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return _service.OnApplicationStartup(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _service.OnApplicationShutdown(cancellationToken);
    }
}
