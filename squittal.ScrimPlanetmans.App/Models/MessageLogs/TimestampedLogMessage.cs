using System;
using Microsoft.AspNetCore.Components;

namespace squittal.ScrimPlanetmans.App.Models.MessageLogs;

public class TimestampedLogMessage
{
    public DateTime Timestamp { get; set; }
    public MarkupString Message { get; set; }

    public TimestampedLogMessage(DateTime timestamp, MarkupString message)
    {
        Timestamp = timestamp;
        Message = message;
    }
}
