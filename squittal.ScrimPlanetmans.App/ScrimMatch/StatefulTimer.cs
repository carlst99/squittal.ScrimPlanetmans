﻿using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Abstractions.Services.ScrimMatch;
using squittal.ScrimPlanetmans.App.ScrimMatch.Events;
using squittal.ScrimPlanetmans.App.ScrimMatch.Interfaces;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch;

public class StatefulTimer : IStatefulTimer
{
    private readonly IScrimMessageBroadcastService _messageService;
    private readonly ILogger<StatefulTimer> _logger;

    private int _secondsMax = 900;
    private int _secondsRemaining;
    private int _secondsElapsed = 0;

    private readonly AutoResetEvent _autoEvent = new AutoResetEvent(true);

    private Timer _timer;

    private bool _isRunning = false;

    // For handling pauses
    private DateTime _prevTickTime;
    private int _resumeDelayMs;

    public event EventHandler<ScrimMessageEventArgs<MatchTimerTickMessage>> RaiseMatchTimerTickEvent;
    public delegate void MatchTimerTickEventHandler(object sender, ScrimMessageEventArgs<MatchTimerTickMessage> e);


    private MatchTimerStatus Status { get; set; } = new MatchTimerStatus();

    public StatefulTimer(IScrimMessageBroadcastService messageService, ILogger<StatefulTimer> logger)
    {
        _messageService = messageService;
        _logger = logger;
        Status.State = MatchTimerState.Initialized;
    }

    public void Configure(TimeSpan timeSpan)
    {
        _logger.LogInformation($"Configuring Timer");

        _autoEvent.WaitOne();
        if (!CanConfigureTimer())
        {
            _logger.LogInformation($"Failed to configure timer: {Enum.GetName(typeof(MatchTimerState), Status.State)}");

            _autoEvent.Set();
            return;
        }

        Status.State = MatchTimerState.Configuring;

        _secondsMax = (int)Math.Round((decimal)timeSpan.TotalSeconds, 0);

        Status.ConfigureTimer(_secondsMax);

        // TODO: move Timer instantiation to the Start() method?
        _timer?.Dispose(); // TODO: is this Dispose necessary?
        _timer = new Timer(HandleTick, _autoEvent, Timeout.Infinite, 1000);

        Status.State = MatchTimerState.Configured;

        _logger.LogInformation($"Timer Configured: {_secondsMax} seconds | {Status.GetSecondsRemaining()} remaining | {Status.GetSecondsElapsed()} elapsed");
            
        // Signal the waiting thread
        _autoEvent.Set();
    }

    public void Start()
    {
        _logger.LogInformation($"Starting Timer");
        // Ensure timer can only be started once
        _autoEvent.WaitOne();
        if (_isRunning || Status.State != MatchTimerState.Configured)
        {
            _logger.LogInformation($"Failed to start timer: {_isRunning.ToString()} | {Enum.GetName(typeof(MatchTimerState), Status.State)}");

            _autoEvent.Set();
            return;
        }

        Status.State = MatchTimerState.Starting;

        // Don't think this is necessary; WaitOne() should already do a Reset  Reset to block other threads, just in case "Start Match" is double-clicked or something
        //_autoEvent.Reset();

        // Immediately start the clock
        // TODO: should we create a new Timer instance instead of having just starting up the single instance?
        _timer.Change(0, 1000);

        _isRunning = true;
        Status.State = MatchTimerState.Running;

        // TODO: broadcast a "MatchStateChange" of event of type "Started" here

        _logger.LogInformation($"Timer Started");

        // Signal the waiting thread
        _autoEvent.Set();

    }

    public void Stop()
    {
        _logger.LogInformation($"Stopping Timer");
        _autoEvent.WaitOne();

        if (Status.State != MatchTimerState.Stopping)
        {
            _logger.LogInformation($"Failed to stop timer: timer not running");
            _autoEvent.Set();
            return;
        }

        // TODO: should we Dispose of the timer instead of putting it on hold indefinitely?
        _timer.Change(Timeout.Infinite, 1000);

        _isRunning = false;
        Status.State = MatchTimerState.Stopped;

        var message = new MatchTimerTickMessage(Status);

        // TODO: use both to make MatchTimer razor component self-contained?
        //OnRaiseMatchTimerTickEvent(new MatchTimerTickEventArgs(message));
        _messageService.BroadcastMatchTimerTickMessage(message);

        // TODO: broadcast a "MatchStateChange" of event of type "Round Ended" here

        _logger.LogInformation($"Timer Stopped");

        _autoEvent.Set();
    }

    public void Pause()
    {
        _logger.LogInformation($"Pausing Timer");

        _autoEvent.WaitOne();
        if (Status.State != MatchTimerState.Running)
        {
            _logger.LogInformation($"Failed to pause timer: timer not running");
            _autoEvent.Set();
            return;
        }

        var now = DateTime.UtcNow;
        _resumeDelayMs = (int)(now - _prevTickTime).TotalMilliseconds;

        _timer.Change(Timeout.Infinite, 1000);

        _isRunning = false;
        Status.State = MatchTimerState.Paused;

        var message = new MatchTimerTickMessage(Status);
            
        // TODO: use both to make MatchTimer razor component self-contained?
        //OnRaiseMatchTimerTickEvent(new MatchTimerTickEventArgs(message));
        _messageService.BroadcastMatchTimerTickMessage(message);

        // TODO: broadcast a "MatchStateChange" of event of type "Round Ended" here

        _logger.LogInformation($"Timer Paused");

        _autoEvent.Set();
    }

    public void Resume()
    {
        _logger.LogInformation($"Resuming Timer");

        _autoEvent.WaitOne();
        if (Status.State != MatchTimerState.Paused)
        {
            _logger.LogInformation($"Failed to resume timer: timer not paused");
            _autoEvent.Set();
            return;
        }

        Status.State = MatchTimerState.Resuming;

        _timer.Change(_resumeDelayMs, 1000);

        _isRunning = true;
        Status.State = MatchTimerState.Running;

        var message = new MatchTimerTickMessage(Status);

        // TODO: use both to make MatchTimer razor component self-contained?
        //OnRaiseMatchTimerTickEvent(new MatchTimerTickEventArgs(message));
        _messageService.BroadcastMatchTimerTickMessage(message);

        // TODO: broadcast a "MatchStateChange" of event of type "Resumed" here

        // Signal the waiting thread
        _autoEvent.Set();
    }

    public void Reset()
    {
        _logger.LogInformation($"Reseting timer");

        if (!CanResetTimer())
        {
            _logger.LogInformation($"Failed to reset timer: {_isRunning.ToString()} | {Enum.GetName(typeof(MatchTimerState), Status.State)}");
            return;
        }

        Configure(TimeSpan.FromSeconds(_secondsMax));

        _logger.LogInformation($"Timer reset");
    }

    public void Halt()
    {
        _logger.LogInformation($"Halting Timer");
        _autoEvent.WaitOne();

        if (!CanHaltTimer())
        {
            _logger.LogInformation($"Failed to halt timer: {Enum.GetName(typeof(MatchTimerState), Status.State)}");
            _autoEvent.Set();
            return;
        }
        Status.State = MatchTimerState.Halting;

        _timer.Change(Timeout.Infinite, 1000);

        _isRunning = false;

        _secondsRemaining = Status.ForceZeroRemaining();
        _secondsElapsed = Status.ForceMaxElapsed();

        Status.State = MatchTimerState.Stopped;

        var message = new MatchTimerTickMessage(Status);

        // TODO: use both to make MatchTimer razor component self-contained?
        //OnRaiseMatchTimerTickEvent(new MatchTimerTickEventArgs(message));
        _messageService.BroadcastMatchTimerTickMessage(message);

        // TODO: broadcast a "MatchStateChange" of event of type "Round Ended" here

        _logger.LogInformation($"Timer Halted");

        _autoEvent.Set();
    }

    private void HandleTick(object stateInfo)
    {
        _logger.LogDebug($"Handling timer tick");

        _autoEvent.WaitOne();

        if (ShouldProcessTick())
        {
            _prevTickTime = DateTime.UtcNow;

            _secondsRemaining = Status.DecrementRemaining();

            _secondsElapsed = Status.IncrementElapsed();

            //Interlocked.Decrement(ref _secondsRemaining);
            //Status.SecondsRemaining = _secondsRemaining;

            //Interlocked.Increment(ref _secondsElapsed);
            //Status.SecondsElapsed = _secondsElapsed;

            var message = new MatchTimerTickMessage(Status);

            if (_secondsRemaining == 0)
            {
                Status.State = MatchTimerState.Stopping;

                // Signal the waiting thread. Only a thread waiting in Stop() should be able to do anything due to _status.State
                _autoEvent.Set();
                Stop();

                return;
            }

            // TODO: use both to make MatchTimer razor component self-contained?
            //OnRaiseMatchTimerTickEvent(new MatchTimerTickEventArgs(message));
            _messageService.BroadcastMatchTimerTickMessage(message);

            // Signal the waiting thread
            _autoEvent.Set();
        }

    }

    private bool ShouldProcessTick()
    {
        return Status.State switch
        {
            MatchTimerState.Running => true,
            MatchTimerState.Starting => false,
            MatchTimerState.Paused => false,
            MatchTimerState.Stopping => false,
            MatchTimerState.Stopped => false,
            MatchTimerState.Halting => false,
            MatchTimerState.Initialized => false,
            MatchTimerState.Resuming => false,
            MatchTimerState.Configuring => false,
            MatchTimerState.Configured => false,
            _ => false,
        };
    }

    private bool CanConfigureTimer()
    {
        if (_isRunning)
        {
            return false;
        }
            
        return Status.State switch
        {
            MatchTimerState.Stopped => true,
            MatchTimerState.Initialized => true,
            MatchTimerState.Configured => true,
            MatchTimerState.Running => false,
            MatchTimerState.Starting => false,
            MatchTimerState.Paused => false,
            MatchTimerState.Stopping => false,
            MatchTimerState.Halting => false,
            MatchTimerState.Resuming => false,
            MatchTimerState.Configuring => false,
            _ => false,
        };
    }

    private bool CanResetTimer()
    {
        if (_isRunning)
        {
            return false;
        }

        return Status.State switch
        {
            MatchTimerState.Stopped => true,
            MatchTimerState.Initialized => false,
            MatchTimerState.Configured => false,
            MatchTimerState.Running => false,
            MatchTimerState.Starting => false,
            MatchTimerState.Paused => false,
            MatchTimerState.Stopping => false,
            MatchTimerState.Halting => false,
            MatchTimerState.Resuming => false,
            MatchTimerState.Configuring => false,
            _ => false,
        };
    }

    private bool CanHaltTimer()
    {
        return Status.State switch
        {
            MatchTimerState.Stopped => true,
            MatchTimerState.Running => true,
            MatchTimerState.Paused => true,
            MatchTimerState.Initialized => false,
            MatchTimerState.Configured => false,
            MatchTimerState.Starting => false,
            MatchTimerState.Stopping => false,
            MatchTimerState.Halting => false,
            MatchTimerState.Resuming => false,
            MatchTimerState.Configuring => false,
            _ => false,
        };
    }

    protected virtual void OnRaiseMatchTimerTickEvent(ScrimMessageEventArgs<MatchTimerTickMessage> e)
    {
        RaiseMatchTimerTickEvent?.Invoke(this, e);
    }


}
