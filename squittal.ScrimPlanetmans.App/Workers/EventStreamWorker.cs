using System;
using System.Threading;
using System.Threading.Tasks;
using DbgCensus.EventStream.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusEventStream;

namespace squittal.ScrimPlanetmans.App.Workers;

public class EventStreamWorker : BackgroundService
{
    private readonly ILogger<EventStreamWorker> _logger;
    private readonly IEventStreamClientFactory _clientFactory;
    private readonly IEventStreamHealthService _healthService;

    private IEventStreamClient? _client;

    public EventStreamWorker
    (
        ILogger<EventStreamWorker> logger,
        IEventStreamClientFactory clientFactory,
        IEventStreamHealthService healthService
    )
    {
        _logger = logger;
        _clientFactory = clientFactory;
        _healthService = healthService;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting event stream client");
        Task healthCheckTask = RunHealthCheckAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            _client = _clientFactory.GetClient();

            try
            {
                await _client.StartAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "An error occurred in the event stream client. Restarting it...");
            }

            _healthService.ResetHealth(_client.Name);

            try
            {
                await Task.Delay(500, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // This is fine
            }
        }

        // This should never error, hence the lazy handling
        await healthCheckTask;
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_client?.IsRunning == true)
            await _client.StopAsync().ConfigureAwait(false);

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RunHealthCheckAsync(CancellationToken ct)
    {
        await Task.Yield();
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(10));

        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (_client is not null && !_healthService.IsHealthy(_client.Name))
                {
                    _logger.LogError(45234, "Census stream has failed health checks. Reconnecting");
                    await _client.ReconnectAsync(ct);
                }

                await timer.WaitForNextTickAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // This is fine
        }
    }
}
