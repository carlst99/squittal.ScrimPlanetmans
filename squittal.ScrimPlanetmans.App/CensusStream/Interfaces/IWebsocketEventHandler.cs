﻿using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace squittal.ScrimPlanetmans.CensusStream;

public interface IWebsocketEventHandler : IDisposable
{
    Task Process(JToken jPayload);
    void DisableScoring();
    void EnabledScoring();
    void EnabledEventStoring();
    void DisableEventStoring();
}
