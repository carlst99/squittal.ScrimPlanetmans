using System;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public class ScrimMessageEventArgs<T> : EventArgs
{
    public ScrimMessageEventArgs(T m)
    {
        Message = m;

        CreatedTime = DateTime.UtcNow;
    }

    public T Message { get; set; }
    public DateTime CreatedTime { get; }
}
