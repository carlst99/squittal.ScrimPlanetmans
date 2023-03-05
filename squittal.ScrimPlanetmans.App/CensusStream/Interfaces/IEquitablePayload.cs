using squittal.ScrimPlanetmans.App.CensusStream.Models;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.CensusStream.Interfaces;

public interface IEquitablePayload<T> : IEquitable<T> where T : PayloadBase
{
}
