using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace squittal.ScrimPlanetmans.App.CensusStream.Interfaces;

public interface IWebsocketEventHandler : IDisposable
{
    Task Process(JToken jPayload);
    void DisableScoring();
    void EnabledScoring();
    void EnabledEventStoring();
    void DisableEventStoring();
}
