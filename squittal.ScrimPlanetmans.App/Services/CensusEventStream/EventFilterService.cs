using System.Collections.Generic;
using DbgCensus.Core.Objects;
using DbgCensus.EventStream.Abstractions.Objects.Events;
using DbgCensus.EventStream.Abstractions.Objects.Events.Characters;
using DbgCensus.EventStream.Abstractions.Objects.Events.Worlds;
using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.Management.Dmf;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusEventStream;

namespace squittal.ScrimPlanetmans.App.Services.CensusEventStream;

/// <inheritdoc />
public class EventFilterService : IEventFilterService
{
    private readonly ILogger<EventFilterService> _logger;

    private readonly HashSet<ulong> _characters;
    private uint? _facility;
    private WorldDefinition? _world;

    /// <inheritdoc />
    public bool IsEventStoringEnabled { get; set; }

    /// <inheritdoc />
    public bool IsScoringEnabled { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EventFilterService"/>.
    /// </summary>
    public EventFilterService(ILogger<EventFilterService> logger)
    {
        _logger = logger;

        _characters = new HashSet<ulong>();
    }

    /// <inheritdoc />
    public void AddCharacter(ulong characterId)
        => _characters.Add(characterId);

    /// <inheritdoc />
    public void RemoveCharacter(ulong characterId)
        => _characters.Remove(characterId);

    /// <inheritdoc />
    public void RemoveAllCharacters()
        => _characters.Clear();

    /// <inheritdoc />
    public void SetFacility(uint facilityId)
    {
        if (_facility != facilityId)
            _logger.LogDebug("Subscribed facility changed: {Old} => {New}", _facility, facilityId);

        _facility = facilityId;
    }

    /// <inheritdoc />
    public void SetWorld(WorldDefinition world)
    {
        if (_world != world)
            _logger.LogDebug("Subscribed world changed: {Old} => {New}", _world, world);

        _world = world;
    }

    /// <inheritdoc />
    public bool IsValidEvent<T>(T e)
        where T : IEvent
    {
        if (e.WorldID != _world)
            return false;

        return e switch
        {
            IAchievementEarned achievementEarned => _characters.Contains(achievementEarned.CharacterID),
            IBattleRankUp battleRankUp => _characters.Contains(battleRankUp.CharacterID),
            IDeath death => _characters.Contains(death.AttackerCharacterID)
                || _characters.Contains(death.CharacterID),
            IGainExperience gainExperience => _characters.Contains(gainExperience.CharacterID),
            IItemAdded itemAdded => _characters.Contains(itemAdded.CharacterID),
            IPlayerFacilityCapture facilityCapture => facilityCapture.FacilityID == _facility
                || _characters.Contains(facilityCapture.CharacterID),
            IPlayerFacilityDefend facilityDefend => facilityDefend.FacilityID == _facility
                || _characters.Contains(facilityDefend.CharacterID),
            IPlayerLogin playerLogin => _characters.Contains(playerLogin.CharacterID),
            IPlayerLogout playerLogout => _characters.Contains(playerLogout.CharacterID),
            ISkillAdded skillAdded => _characters.Contains(skillAdded.CharacterID),
            IVehicleDestroy vehicleDestroy => vehicleDestroy.FacilityID == _facility
                || _characters.Contains(vehicleDestroy.AttackerCharacterID)
                || _characters.Contains(vehicleDestroy.CharacterID),
            IFacilityControl facilityControl => facilityControl.FacilityID == _facility,
            _ => throw new InvalidOperandException($"The event type {typeof(T)} has not been filtered")
        };
    }
}
