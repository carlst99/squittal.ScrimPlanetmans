using System;
using Microsoft.AspNetCore.Components;

namespace squittal.ScrimPlanetmans.App.Models.MessageLogs;

public record TimestampedLogMessage(DateTime Timestamp, MarkupString Message);
