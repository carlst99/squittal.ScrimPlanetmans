using DbgCensus.EventStream.Abstractions.Objects.Events;

namespace squittal.ScrimPlanetmans.App.CensusStream.Interfaces;

/// <summary>
/// Represents a service for tracking the health of an event stream connection.
/// </summary>
public interface IEventStreamHealthService
{
    /// <summary>
    /// Gets a value indicating whether or not a client is healthy.
    /// </summary>
    /// <param name="clientName">The name of the client.</param>
    /// <returns><c>True</c> if the client is healthy, otherwise <c>false</c>.</returns>
    bool IsHealthy(string clientName);

    /// <summary>
    /// Pushes an event to the health service.
    /// </summary>
    /// <typeparam name="T">The event type.</typeparam>
    /// <param name="clientName">The name of the client from which the event was received.</param>
    /// <param name="receivedEvent">The received event.</param>
    void PushReceivedEvent<T>(string clientName, T receivedEvent)
        where T : IEvent;

    /// <summary>
    /// Resets the health status of a client.
    /// </summary>
    /// <param name="clientName">The name of the client.</param>
    void ResetHealth(string clientName);
}
