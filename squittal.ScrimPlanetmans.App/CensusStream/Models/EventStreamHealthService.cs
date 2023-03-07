// Credit to Lampjaw @ Voidwell.DaybreakGames

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using DbgCensus.EventStream.Abstractions.Objects.Events;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.CensusStream.Interfaces;

namespace squittal.ScrimPlanetmans.App.CensusStream.Models;

/// <inheritdoc />
public class EventStreamHealthService : IEventStreamHealthService
{
    private readonly ILogger<EventStreamHealthService> _logger;

    private readonly ConcurrentDictionary<int, ConcurrentDictionary<string, DateTime>> _worldsLastSeenEvents = new ConcurrentDictionary<int, ConcurrentDictionary<string, DateTime>>();

    private readonly List<int> _ignorableWorlds = new List<int> { 19, 25 }; // Jaeger, Briggs
    private readonly Dictionary<string, TimeSpan> _unhealthyEventIntervals = new Dictionary<string, TimeSpan>
    {
        {  "Death", TimeSpan.FromMinutes(5) }
    };

    public EventStreamHealthService(ILogger<EventStreamHealthService> logger)
    {
        _logger = logger;
    }

    public void ReceivedEvent(int worldId, string eventName, DateTime? timestamp = null)
    {
        if (timestamp == null)
        {
            timestamp = DateTime.UtcNow;
        }

        try
        {
            if (!_worldsLastSeenEvents.ContainsKey(worldId))
            {
                _worldsLastSeenEvents.TryAdd(worldId, new ConcurrentDictionary<string, DateTime>());
            }

            _worldsLastSeenEvents[worldId].AddOrUpdate(eventName, timestamp.Value, (k, v) => timestamp.Value);
        }
        catch (Exception ex)
        {
            var timeDisplay = timestamp == null ? "unknown" : ((DateTime)timestamp).ToLongTimeString();
            _logger.LogError($"Failed to update world state from event eventName={eventName} worldId={worldId} time={timeDisplay}: {ex}");
        }
    }

    public void ClearWorld(int worldId)
    {
        _worldsLastSeenEvents.TryRemove(worldId, out var _);
    }

    public void ResetHealth()
    {
        _worldsLastSeenEvents.Clear();
    }

    public bool IsHealthy()
    {
        var worldIds = _worldsLastSeenEvents.Keys.Where(a => !_ignorableWorlds.Contains(a)).ToList();

        return !worldIds.Where(a => !TryEvaluateWorldHealth(a)).Any();
    }

    private bool TryEvaluateWorldHealth(int worldId)
    {
        if (_worldsLastSeenEvents.TryGetValue(worldId, out var eventList))
        {
            if (!EvaluateWorldHealth(eventList))
            {
                _logger.LogWarning(34214, "Stream for world '{worldId}' failed health check", worldId);
                return false;
            }
        }
        return true;
    }

    private bool EvaluateWorldHealth(ConcurrentDictionary<string, DateTime> worldEvents)
    {
        var now = DateTime.UtcNow;

        foreach ((var eventName, var interval) in _unhealthyEventIntervals)
        {
            if (worldEvents != null && worldEvents.TryGetValue(eventName, out var lastReceivedTime))
            {
                if (now - lastReceivedTime > interval)
                {
                    return false;
                }
            }
        }

        return true;
    }

    // TODO: 
    public bool IsHealthy(string clientName)
    {
        return true;
    }

    public void PushReceivedEvent<T>(string clientName, T receivedEvent) where T : IEvent
    {
    }

    public void ResetHealth(string clientName)
    {
    }
}
