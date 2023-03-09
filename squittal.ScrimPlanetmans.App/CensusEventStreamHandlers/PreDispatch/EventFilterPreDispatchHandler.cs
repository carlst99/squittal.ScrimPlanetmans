using System.Threading;
using System.Threading.Tasks;
using DbgCensus.EventStream.Abstractions.Objects;
using DbgCensus.EventStream.Abstractions.Objects.Events;
using DbgCensus.EventStream.EventHandlers.Abstractions;
using DbgCensus.EventStream.EventHandlers.Abstractions.Objects;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusEventStream;

namespace squittal.ScrimPlanetmans.App.CensusEventStreamHandlers.PreDispatch;

/// <summary>
/// A pre-dispatch handler to filter out unwanted payloads.
/// </summary>
public class EventFilterPreDispatchHandler : IPreDispatchHandler
{
    private readonly IPayloadContext _context;
    private readonly IEventFilterService _eventFilter;
    private readonly IEventStreamHealthService _healthService;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventFilterPreDispatchHandler"/> class.
    /// </summary>
    /// <param name="context">The payload context.</param>
    /// <param name="eventFilter">The event filter service.</param>
    /// <param name="healthService">The event stream health service.</param>
    public EventFilterPreDispatchHandler
    (
        IPayloadContext context,
        IEventFilterService eventFilter,
        IEventStreamHealthService healthService
    )
    {
        _context = context;
        _eventFilter = eventFilter;
        _healthService = healthService;
    }

    /// <inheritdoc />
    public ValueTask<bool> HandlePayloadAsync<T>(T payload, CancellationToken ct = default)
        where T : IPayload
    {
        if (payload is not IEvent e)
            return ValueTask.FromResult(false);

        _healthService.PushReceivedEvent(_context.DispatchingClientName, e);
        return ValueTask.FromResult(!_eventFilter.IsValidEvent(e));

    }
}
