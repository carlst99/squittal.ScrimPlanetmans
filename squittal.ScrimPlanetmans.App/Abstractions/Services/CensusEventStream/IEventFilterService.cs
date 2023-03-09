using DbgCensus.Core.Objects;
using DbgCensus.EventStream.Abstractions.Objects.Events;

namespace squittal.ScrimPlanetmans.App.Abstractions.Services.CensusEventStream;

public interface IEventFilterService
{
    bool IsEventStoringEnabled { get; set; }
    bool IsScoringEnabled { get; set; }

    void AddCharacter(ulong characterId);
    void RemoveCharacter(ulong characterId);
    void RemoveAllCharacters();
    void SetFacility(uint facilityId);
    void SetWorld(WorldDefinition world);

    bool IsValidEvent<T>(T e)
        where T : IEvent;
}
