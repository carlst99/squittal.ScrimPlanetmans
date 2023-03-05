using System.Collections.Generic;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models;

namespace squittal.ScrimPlanetmans.App.CensusStream.Interfaces;

public interface IWebsocketMonitor : IStatefulHostedService
{
    void AddCharacterSubscriptions(IEnumerable<string> characterIds);
    void RemoveCharacterSubscription(string characterId);
    void RemoveCharacterSubscriptions(IEnumerable<string> characterIds);
    void RemoveAllCharacterSubscriptions();
    Task<ServiceState> GetStatus();
    void EnableScoring();
    void DisableScoring();
    void AddCharacterSubscription(string characterId);
    void SetFacilitySubscription(int facilityId);
    void SetWorldSubscription(int worldId);
    void EnableEventStoring();
    void DisableEventStoring();
}
