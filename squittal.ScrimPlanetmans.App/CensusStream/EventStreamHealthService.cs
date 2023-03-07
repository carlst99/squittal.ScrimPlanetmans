// Credit to Lampjaw @ Voidwell.DaybreakGames

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using DbgCensus.Core.Objects;
using DbgCensus.EventStream.Abstractions.Objects.Events;
using DbgCensus.EventStream.Abstractions.Objects.Events.Characters;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.CensusStream.Interfaces;

namespace squittal.ScrimPlanetmans.App.CensusStream;

/// <inheritdoc />
public class EventStreamHealthService : IEventStreamHealthService
{
    private static readonly IReadOnlyDictionary<Type, TimeSpan> UNHEALTHY_EVENT_INTERVALS = new Dictionary<Type, TimeSpan>
    {
        {  typeof(IDeath), TimeSpan.FromMinutes(5) }
    };

    private static readonly HashSet<WorldDefinition> IGNORABLE_WORLDS = new()
    {
        WorldDefinition.Jaeger,
        WorldDefinition.Briggs
    };

    private readonly ILogger<EventStreamHealthService> _logger;
    private readonly ConcurrentDictionary<WorldDefinition, ConcurrentDictionary<string, DateTime>> _worldsLastSeenEvents = new();

    public EventStreamHealthService(ILogger<EventStreamHealthService> logger)
    {
        _logger = logger;
    }

    // TODO:
    public bool IsHealthy(string clientName)
    {
        return true;
    }

    public void PushReceivedEvent<T>(string clientName, T receivedEvent)
        where T : IEvent
    {
    }

    public void ResetHealth(string clientName)
    {
    }
}
