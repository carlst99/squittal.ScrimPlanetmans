using System;
using squittal.ScrimPlanetmans.App.ScrimMatch.Events;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Interfaces;

public interface IStatefulTimer
{
    event EventHandler<ScrimMessageEventArgs<MatchTimerTickMessage>> RaiseMatchTimerTickEvent;

    void Configure(TimeSpan timeSpan);
    void Start();
    void Pause();
    void Reset();
    void Stop();
    void Halt();
    void Resume();
}
