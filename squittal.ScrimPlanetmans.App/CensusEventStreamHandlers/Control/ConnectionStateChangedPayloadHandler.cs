using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DbgCensus.EventStream.Abstractions;
using DbgCensus.EventStream.Abstractions.Objects;
using DbgCensus.EventStream.Abstractions.Objects.Control;
using DbgCensus.EventStream.Abstractions.Objects.Events;
using DbgCensus.EventStream.EventHandlers.Abstractions;
using DbgCensus.EventStream.EventHandlers.Abstractions.Objects;
using DbgCensus.EventStream.Objects.Commands;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusEventStream;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.CensusEventStreamHandlers.Control;

public class ConnectionStateChangedPayloadHandler : IPayloadHandler<IConnectionStateChanged>
{
    private readonly ILogger<ConnectionStateChangedPayloadHandler> _logger;
    private readonly IPayloadContext _context;
    private readonly IEventStreamClientFactory _clientFactory;
    private readonly IEventStreamHealthService _healthService;

    public ConnectionStateChangedPayloadHandler
    (
        ILogger<ConnectionStateChangedPayloadHandler> logger,
        IPayloadContext context,
        IEventStreamClientFactory clientFactory,
        IEventStreamHealthService healthService
    )
    {
        _logger = logger;
        _context = context;
        _clientFactory = clientFactory;
        _healthService = healthService;
    }

    public async Task HandleAsync(IConnectionStateChanged payload, CancellationToken ct = default)
    {
        _logger.LogWarning
        (
            "Event stream connection state changed for the client {ClientName}: we are now {State}!",
            _context.DispatchingClientName,
            payload.Connected ? "connected" : "disconnected"
        );

        if (!payload.Connected)
        {
            _healthService.ResetHealth(_context.DispatchingClientName);
            return;
        }

        List<string> eventNames = new()
        {
            EventNames.Death,
            EventNames.FacilityControl,
            EventNames.PlayerLogin,
            EventNames.PlayerLogout,
            EventNames.VehicleDestroy
        };
        eventNames.AddRange(ExperienceEventsBuilder.GetExperienceEvents());

        IEventStreamClient client = _clientFactory.GetClient(_context.DispatchingClientName);
        await client.SendCommandAsync
        (
            new Subscribe
            (
                new All(),
                eventNames,
                Worlds: new All()
            ),
            ct
        ).ConfigureAwait(false);
    }
}
