using System.Threading;
using System.Threading.Tasks;
using DbgCensus.EventStream.Abstractions.Objects.Events.Worlds;
using DbgCensus.EventStream.EventHandlers.Abstractions;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusEventStream;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusRest;
using squittal.ScrimPlanetmans.App.Data;
using squittal.ScrimPlanetmans.App.Data.Models;
using squittal.ScrimPlanetmans.App.Models.CensusRest;
using squittal.ScrimPlanetmans.App.ScrimMatch;
using squittal.ScrimPlanetmans.App.ScrimMatch.Events;
using squittal.ScrimPlanetmans.App.ScrimMatch.Interfaces;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;
using squittal.ScrimPlanetmans.App.Services.ScrimMatch.Interfaces;

namespace squittal.ScrimPlanetmans.App.CensusEventStreamHandlers;

public class FacilityControlEventHandler : IPayloadHandler<IFacilityControl>
{
    private readonly ILogger<FacilityControlEventHandler> _logger;
    private readonly IEventFilterService _eventFilter;
    private readonly ICensusMapRegionService _mapRegionService;
    private readonly IScrimTeamsManager _teamsManager;
    private readonly IScrimMessageBroadcastService _messageService;
    private readonly IScrimMatchScorer _scorer;
    private readonly IScrimMatchDataService _scrimMatchService;
    private readonly PlanetmansDbContext _dbContext;

    public FacilityControlEventHandler
    (
        ILogger<FacilityControlEventHandler> logger,
        IEventFilterService eventFilter,
        ICensusMapRegionService mapRegionService,
        IScrimTeamsManager teamsManager,
        IScrimMessageBroadcastService messageService,
        IScrimMatchScorer scorer,
        IScrimMatchDataService scrimMatchService,
        PlanetmansDbContext dbContext
    )
    {
        _logger = logger;
        _eventFilter = eventFilter;
        _mapRegionService = mapRegionService;
        _teamsManager = teamsManager;
        _messageService = messageService;
        _scorer = scorer;
        _scrimMatchService = scrimMatchService;
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task HandleAsync(IFacilityControl payload, CancellationToken ct = new())
    {
        int oldFactionId = (int)payload.OldFactionID;
        int newFactionId = (int)payload.NewFactionID;
        FacilityControlType type = GetFacilityControlType(oldFactionId, newFactionId);

        if (type is FacilityControlType.Unknown)
        {
            _logger.LogInformation
            (
                "FacilityControl payload for facility {ID} had unknown FacilityControlType. " +
                "Old faction: {Old}, new faction: {New}",
                payload.FacilityID,
                payload.OldFactionID,
                payload.NewFactionID
            );
            return;
        }

        Team? controllingTeam = _teamsManager.GetFirstTeamWithFactionId(newFactionId);
        if (controllingTeam is null)
        {
            _logger.LogInformation
            (
                "Could not resolve controlling team for {Type} FacilityControl payload: facilityId={FacilityId} newFactionId={NewFactionId} oldFactionId={OldFactionId}",
                type,
                payload.FacilityID,
                payload.NewFactionID,
                payload.OldFactionID
            );
            return;
        }

        ScrimActionType actionType = GetFacilityControlActionType(type);

        _logger.LogInformation
        (
            "FacilityControl payload has FacilityControlType of {Type} & ScrimActionType of {ActionType} " +
            "for Team {Ordinal}: worldId={WorldId} facilityId={FacilityId} " +
            "newFactionId={NewFactionId} oldFactionId={OldFactionId}",
            type,
            actionType,
            controllingTeam.TeamOrdinal,
            payload.WorldID,
            payload.FacilityID,
            payload.NewFactionID,
            payload.OldFactionID
        );

        // "Outside Influence" doesn't really apply to base captures
        if (actionType == ScrimActionType.None)
            return;

        CensusMapRegion? mapRegion = await _mapRegionService.GetByFacilityIdAsync(payload.FacilityID, ct);

        ScrimFacilityControlActionEvent controlEvent = new()
        {
            FacilityId = (int)payload.FacilityID,
            FacilityName = mapRegion?.FacilityName,
            NewFactionId = (int)payload.NewFactionID,
            OldFactionId = (int)payload.OldFactionID,
            DurationHeld = (int)payload.DurationHeld,
            OutfitId = payload.OutfitID.ToString(),
            Timestamp = payload.Timestamp.UtcDateTime,
            WorldId = (int)payload.WorldID,
            ZoneId = (int)payload.ZoneID.CombinedId,
            ControllingTeamOrdinal = controllingTeam.TeamOrdinal,
            ControlType = type,
            ActionType = actionType
        };

        if (_eventFilter.IsScoringEnabled)
        {
            ScrimEventScoringResult scoringResult = _scorer.ScoreFacilityControlEvent(controlEvent);
            controlEvent.Points = scoringResult.Points;
            controlEvent.IsBanned = scoringResult.IsBanned;

            string currentMatchId = _scrimMatchService.CurrentMatchId;
            int currentRound = _scrimMatchService.CurrentMatchRound;

            if (_eventFilter.IsEventStoringEnabled && !string.IsNullOrWhiteSpace(currentMatchId))
            {
                ScrimFacilityControl dataModel = new()
                {
                    ScrimMatchId = currentMatchId,
                    Timestamp = controlEvent.Timestamp,
                    ScrimMatchRound = currentRound,
                    ControllingTeamOrdinal = controlEvent.ControllingTeamOrdinal,
                    ActionType = controlEvent.ActionType,
                    ControlType = controlEvent.ControlType,
                    Points = controlEvent.Points,
                    FacilityId = controlEvent.FacilityId
                };

                _dbContext.ScrimFacilityControls.Add(dataModel);
                await _dbContext.SaveChangesAsync(ct);
            }
        }

        _messageService.BroadcastScrimFacilityControlActionEventMessage
        (
            new ScrimFacilityControlActionEventMessage(controlEvent)
        );
    }

    private static FacilityControlType GetFacilityControlType(int oldFactionId, int newFactionId)
    {
        if (newFactionId is 0)
            return FacilityControlType.Unknown;

        return oldFactionId == newFactionId
            ? FacilityControlType.Defense
            : FacilityControlType.Capture;
    }

    private ScrimActionType GetFacilityControlActionType(FacilityControlType type)
    {
        if (type is FacilityControlType.Unknown)
            return ScrimActionType.None;

        int matchRoundControlVictories = _teamsManager.GetCurrentMatchRoundBaseControlsCount();

        // Only the first defense in a round should ever count. After that, base always trades hands via captures
        if (type is FacilityControlType.Defense && matchRoundControlVictories != 0)
            return ScrimActionType.None;

        return matchRoundControlVictories == 0
            ? ScrimActionType.FirstBaseCapture
            : ScrimActionType.SubsequentBaseCapture;
    }
}
