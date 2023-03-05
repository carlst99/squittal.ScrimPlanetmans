// Credit to Lampjaw @ Voidwell.DaybreakGames

using System;
using System.Threading.Tasks;
using DaybreakGames.Census.Stream;
using Websocket.Client;

namespace squittal.ScrimPlanetmans.App.CensusStream.Interfaces;

public interface IStreamClient : IDisposable
{
    StreamClient OnDisconnect(Func<DisconnectionInfo, Task> onDisconnect);
    StreamClient OnMessage(Func<string, Task> onMessage);
    Task ConnectAsync(CensusStreamSubscription subscription);
    Task DisconnectAsync();
    Task ReconnectAsync();
}
