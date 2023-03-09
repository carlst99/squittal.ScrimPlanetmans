using System;
using System.Threading;
using System.Threading.Tasks;
using DbgCensus.EventStream.Abstractions.Objects.Events.Characters;
using DbgCensus.EventStream.EventHandlers.Abstractions;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusEventStream;
using squittal.ScrimPlanetmans.App.Data;
using squittal.ScrimPlanetmans.App.Data.Models;
using squittal.ScrimPlanetmans.App.ScrimMatch.Events;
using squittal.ScrimPlanetmans.App.ScrimMatch.Interfaces;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;
using squittal.ScrimPlanetmans.App.Services.Planetside;
using squittal.ScrimPlanetmans.App.Services.ScrimMatch.Interfaces;

namespace squittal.ScrimPlanetmans.App.CensusEventStreamHandlers;

public class GainExperiencePayloadHandler : IPayloadHandler<IGainExperience>
{
    private readonly ILogger<GainExperiencePayloadHandler> _logger;
    private readonly IEventFilterService _eventFilter;
    private readonly IScrimTeamsManager _teamsManager;
    private readonly IScrimMessageBroadcastService _messageService;
    private readonly IScrimMatchScorer _scorer;
    private readonly IScrimMatchDataService _scrimMatchService;
    private readonly PlanetmansDbContext _dbContext;

    public GainExperiencePayloadHandler
    (
        ILogger<GainExperiencePayloadHandler> logger,
        IEventFilterService eventFilter,
        IScrimTeamsManager teamsManager,
        IScrimMessageBroadcastService messageService,
        IScrimMatchScorer scorer,
        IScrimMatchDataService scrimMatchService,
        PlanetmansDbContext dbContext
    )
    {
        _logger = logger;
        _eventFilter = eventFilter;
        _teamsManager = teamsManager;
        _messageService = messageService;
        _scorer = scorer;
        _scrimMatchService = scrimMatchService;
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task HandleAsync(IGainExperience payload, CancellationToken ct = new())
    {
        int experienceId = (int)payload.ExperienceID;
        ExperienceType experienceType = ExperienceEventsBuilder.GetExperienceTypeFromId(experienceId);

        ScrimExperienceGainActionEvent baseEvent = new()
        {
            Timestamp = payload.Timestamp.UtcDateTime,
            ZoneId = (int)payload.ZoneID.CombinedId,
            ExperienceType = experienceType,
            ExperienceGainInfo = new ScrimActionExperienceGainInfo(experienceId, (int)payload.Amount),
            LoadoutId = (int)payload.LoadoutID
        };

        try
        {
            switch (experienceType)
            {
                case ExperienceType.Revive:
                    await ProcessRevivePayloadAsync(baseEvent, payload, ct);
                    return;

                case ExperienceType.DamageAssist:
                    await ProcessAssistPayloadAsync(baseEvent, payload, ct);
                    return;

                case ExperienceType.UtilityAssist:
                    //ProcessAssistPayload(baseEvent, payload);
                    return;

                case ExperienceType.PointControl:
                    await ProcessPointControlPayload(baseEvent, payload, ct);
                    return;

                case ExperienceType.GrenadeAssist:
                    await ProcessAssistPayloadAsync(baseEvent, payload, ct);
                    return;

                case ExperienceType.HealSupportAssist:
                    await ProcessAssistPayloadAsync(baseEvent, payload, ct);
                    return;

                case ExperienceType.ProtectAlliesAssist:
                    await ProcessAssistPayloadAsync(baseEvent, payload, ct);
                    return;

                case ExperienceType.SpotAssist:
                    await ProcessAssistPayloadAsync(baseEvent, payload, ct);
                    return;

                default:
                    return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process a GainExperience payload");
        }
    }

    private async Task ProcessRevivePayloadAsync
    (
        ScrimExperienceGainActionEvent baseEvent,
        IGainExperience payload,
        CancellationToken ct
    )
    {
        // Not much value in a revive that we only have one end of
        if (payload.CharacterID is 0 || payload.OtherID is 0)
            return;

        ulong medicId = payload.CharacterID;
        ulong revivedId = payload.OtherID;
        bool involvesBenchedPlayer = false;

        ScrimReviveActionEvent reviveEvent = new(baseEvent, medicId, revivedId);

        Player? medicPlayer = _teamsManager.GetPlayerFromId(medicId);
        reviveEvent.MedicPlayer = medicPlayer;

        if (medicPlayer is not null)
        {
            _teamsManager.SetPlayerLoadoutId(medicId, reviveEvent.LoadoutId);
            involvesBenchedPlayer = involvesBenchedPlayer || medicPlayer.IsBenched;
        }

        Player? revivedPlayer = _teamsManager.GetPlayerFromId(revivedId);
        reviveEvent.RevivedPlayer = revivedPlayer;

        if (revivedPlayer is not null)
            involvesBenchedPlayer = involvesBenchedPlayer || revivedPlayer.IsBenched;

        reviveEvent.ActionType = GetReviveScrimActionType(reviveEvent);

        if (reviveEvent.ActionType != ScrimActionType.OutsideInterference)
        {
            if (_eventFilter.IsScoringEnabled && !involvesBenchedPlayer)
            {
                ScrimEventScoringResult scoringResult = await _scorer.ScoreReviveEventAsync(reviveEvent, ct);
                reviveEvent.Points = scoringResult.Points;
                reviveEvent.IsBanned = scoringResult.IsBanned;

                string currentMatchId = _scrimMatchService.CurrentMatchId;
                int currentRound = _scrimMatchService.CurrentMatchRound;

                if (_eventFilter.IsEventStoringEnabled && !string.IsNullOrWhiteSpace(currentMatchId))
                {
                    ScrimRevive dataModel = new()
                    {
                        ScrimMatchId = currentMatchId,
                        Timestamp = reviveEvent.Timestamp,
                        MedicCharacterId = medicId,
                        RevivedCharacterId = revivedId,
                        ScrimMatchRound = currentRound,
                        ActionType = reviveEvent.ActionType,
                        MedicTeamOrdinal = reviveEvent.MedicPlayer?.TeamOrdinal,
                        RevivedTeamOrdinal = reviveEvent.RevivedPlayer?.TeamOrdinal,
                        MedicLoadoutId = reviveEvent.MedicPlayer?.LoadoutId,
                        RevivedLoadoutId = reviveEvent.RevivedPlayer?.LoadoutId,
                        ExperienceGainId = reviveEvent.ExperienceGainInfo.Id,
                        ExperienceGainAmount = reviveEvent.ExperienceGainInfo.Amount,
                        ZoneId = (int)payload.ZoneID.CombinedId,
                        WorldId = (int)payload.WorldID,
                        Points = reviveEvent.Points,
                    };

                    _dbContext.ScrimRevives.Add(dataModel);
                    await _dbContext.SaveChangesAsync(ct);
                }
            }
        }

        _messageService.BroadcastScrimReviveActionEventMessage(new ScrimReviveActionEventMessage(reviveEvent));
    }

    private static ScrimActionType GetReviveScrimActionType(ScrimReviveActionEvent reviveEvent)
    {
        // Determine if this is involves a non-tracked player
        if (reviveEvent.MedicPlayer is null || reviveEvent.RevivedPlayer is null)
            return ScrimActionType.OutsideInterference;

        bool isRevivedMax = false;

        if (reviveEvent.RevivedPlayer is not null)
            isRevivedMax = ProfileService.IsMaxLoadoutId(reviveEvent.RevivedPlayer.LoadoutId);

        return isRevivedMax
            ? ScrimActionType.ReviveMax
            : ScrimActionType.ReviveInfantry;
    }

    private async Task ProcessAssistPayloadAsync
    (
        ScrimExperienceGainActionEvent baseEvent,
        IGainExperience payload,
        CancellationToken ct
    )
    {
        // Sanity check
        if (payload.CharacterID is 0)
            return;

        ulong attackerId = payload.CharacterID;
        ulong? victimId = payload.OtherID is 0 ? null : payload.OtherID;
        bool involvesBenchedPlayer = false;

        Player? attackerPlayer = _teamsManager.GetPlayerFromId(attackerId);
        Player? victimPlayer = null;
        if (victimId is not null)
            victimPlayer = _teamsManager.GetPlayerFromId(victimId.Value);

        // No point broadcasting an assist event for two characters we're not tracking
        if (attackerPlayer is null && victimPlayer is null)
            return;

        ScrimAssistActionEvent assistEvent = new
        (
            baseEvent,
            attackerId,
            attackerPlayer,
            victimId,
            victimPlayer
        );

        if (attackerPlayer is not null)
        {
            _teamsManager.SetPlayerLoadoutId(attackerId, assistEvent.LoadoutId);
            involvesBenchedPlayer = involvesBenchedPlayer || attackerPlayer.IsBenched;
        }

        if (victimPlayer is not null)
            involvesBenchedPlayer = involvesBenchedPlayer || victimPlayer.IsBenched;

        assistEvent.ActionType = GetAssistScrimActionType(assistEvent);
        if (assistEvent.ActionType != ScrimActionType.OutsideInterference)
        {
            if (_eventFilter.IsScoringEnabled && !involvesBenchedPlayer)
            {
                ScrimEventScoringResult scoringResult = await _scorer.ScoreAssistEventAsync(assistEvent, ct);
                assistEvent.Points = scoringResult.Points;
                assistEvent.IsBanned = scoringResult.IsBanned;

                string currentMatchId = _scrimMatchService.CurrentMatchId;
                int currentRound = _scrimMatchService.CurrentMatchRound;

                if (_eventFilter.IsEventStoringEnabled && !string.IsNullOrWhiteSpace(currentMatchId))
                {
                    switch (assistEvent.ActionType)
                    {
                        case ScrimActionType.DamageAssist:
                            await SaveScrimDamageAssistToDbAsync
                            (
                                assistEvent,
                                currentMatchId,
                                currentRound,
                                ct
                            );
                            break;

                        case ScrimActionType.GrenadeAssist:
                            await SaveScrimGrenadeAssistToDbAsync
                            (
                                assistEvent,
                                currentMatchId,
                                currentRound,
                                ct
                            );
                            break;

                        case ScrimActionType.SpotAssist:
                            await SaveScrimSpotAssistToDbAsync
                            (
                                assistEvent,
                                currentMatchId,
                                currentRound,
                                ct
                            );
                            break;
                    }
                }
            }
        }

        _messageService.BroadcastScrimAssistActionEventMessage(new ScrimAssistActionEventMessage(assistEvent));
    }

    private static ScrimActionType GetAssistScrimActionType(ScrimAssistActionEvent assistEvent)
    {
        // Determine if this is involves a non-tracked player
        if (assistEvent.AttackerPlayer is null || assistEvent.VictimPlayer is null)
            return ScrimActionType.OutsideInterference;

        bool isTeamkillAssist = assistEvent.AttackerPlayer != null
            && assistEvent.VictimPlayer != null
            && assistEvent.AttackerPlayer.TeamOrdinal == assistEvent.VictimPlayer.TeamOrdinal
            && assistEvent.AttackerPlayer != assistEvent.VictimPlayer;

        bool isSuicideAssist = assistEvent.AttackerPlayer != null
            && assistEvent.VictimPlayer != null
            && assistEvent.AttackerPlayer.TeamOrdinal == assistEvent.VictimPlayer.TeamOrdinal
            && assistEvent.AttackerPlayer == assistEvent.VictimPlayer;

        return assistEvent.ExperienceType switch
        {
            ExperienceType.DamageAssist => (!(isTeamkillAssist || isSuicideAssist) ? ScrimActionType.DamageAssist
                : (isTeamkillAssist ? ScrimActionType.DamageTeamAssist
                    : ScrimActionType.DamageSelfAssist)),
            ExperienceType.GrenadeAssist => (!(isTeamkillAssist || isSuicideAssist) ? ScrimActionType.GrenadeAssist
                : (isTeamkillAssist ? ScrimActionType.GrenadeTeamAssist
                    : ScrimActionType.GrenadeSelfAssist)),
            ExperienceType.HealSupportAssist => ScrimActionType.HealSupportAssist,
            ExperienceType.ProtectAlliesAssist => ScrimActionType.ProtectAlliesAssist,
            ExperienceType.SpotAssist => ScrimActionType.SpotAssist,
            _ => ScrimActionType.UtilityAssist
        };
    }

    private async Task SaveScrimDamageAssistToDbAsync
    (
        ScrimAssistActionEvent assistEvent,
        string matchId,
        int matchRound,
        CancellationToken ct
    )
    {
        ScrimDamageAssist dataModel = new()
        {
            ScrimMatchId = matchId,
            Timestamp = assistEvent.Timestamp,
            AttackerCharacterId = assistEvent.AttackerPlayer?.Id,
            VictimCharacterId = assistEvent.VictimPlayer?.Id,
            ScrimMatchRound = matchRound,
            ActionType = assistEvent.ActionType,
            AttackerTeamOrdinal = assistEvent.AttackerPlayer?.TeamOrdinal,
            VictimTeamOrdinal = assistEvent.VictimPlayer?.TeamOrdinal,
            Points = assistEvent.Points,
        };

        _dbContext.ScrimDamageAssists.Add(dataModel);
        await _dbContext.SaveChangesAsync(ct);
    }

    private async Task SaveScrimGrenadeAssistToDbAsync
    (
        ScrimAssistActionEvent assistEvent,
        string matchId,
        int matchRound,
        CancellationToken ct
    )
    {
        ScrimGrenadeAssist dataModel = new()
        {
            ScrimMatchId = matchId,
            Timestamp = assistEvent.Timestamp,
            AttackerCharacterId = assistEvent.AttackerPlayer?.Id,
            VictimCharacterId = assistEvent.VictimPlayer?.Id,
            ScrimMatchRound = matchRound,
            ActionType = assistEvent.ActionType,
            AttackerTeamOrdinal = assistEvent.AttackerPlayer?.TeamOrdinal,
            VictimTeamOrdinal = assistEvent.VictimPlayer?.TeamOrdinal,
            Points = assistEvent.Points,
        };

        _dbContext.ScrimGrenadeAssists.Add(dataModel);
        await _dbContext.SaveChangesAsync(ct);
    }

    private async Task SaveScrimSpotAssistToDbAsync
    (
        ScrimAssistActionEvent assistEvent,
        string matchId,
        int matchRound,
        CancellationToken ct
    )
    {
        ScrimSpotAssist dataModel = new()
        {
            ScrimMatchId = matchId,
            Timestamp = assistEvent.Timestamp,
            SpotterCharacterId = assistEvent.AttackerPlayer?.Id,
            VictimCharacterId = assistEvent.VictimPlayer?.Id,
            ScrimMatchRound = matchRound,
            ActionType = assistEvent.ActionType,
            SpotterTeamOrdinal = assistEvent.AttackerPlayer?.TeamOrdinal,
            VictimTeamOrdinal = assistEvent.VictimPlayer?.TeamOrdinal,
            Points = assistEvent.Points,
        };

        _dbContext.ScrimSpotAssists.Add(dataModel);
        await _dbContext.SaveChangesAsync(ct);
    }

    private async Task ProcessPointControlPayload
    (
        ScrimExperienceGainActionEvent baseEvent,
        IGainExperience payload,
        CancellationToken ct
    )
    {
        // Sanity check
        if (payload.CharacterID is 0)
            return;

        ulong playerId = payload.CharacterID;
        Player? player = _teamsManager.GetPlayerFromId(playerId);

        ScrimObjectiveTickActionEvent controlEvent = new(baseEvent, playerId)
        {
            Player = player
        };

        if (player is not null)
            _teamsManager.SetPlayerLoadoutId(playerId, controlEvent.LoadoutId);

        controlEvent.ActionType = GetObjectiveTickScrimActionType(controlEvent);
        if (controlEvent.ActionType != ScrimActionType.Unknown)
        {
            if (_eventFilter.IsScoringEnabled && player?.IsBenched is false)
            {
                ScrimEventScoringResult scoringResult = await _scorer.ScoreObjectiveTickEventAsync(controlEvent, ct);
                controlEvent.Points = scoringResult.Points;
                controlEvent.IsBanned = scoringResult.IsBanned;
            }
        }

        _messageService.BroadcastScrimObjectiveTickActionEventMessage(new ScrimObjectiveTickActionEventMessage(controlEvent));
    }

    private static ScrimActionType GetObjectiveTickScrimActionType(ScrimExperienceGainActionEvent controlEvent)
    {
        int experienceId = controlEvent.ExperienceGainInfo.Id;

        return experienceId switch
        {
            15 => ScrimActionType.PointControl,             // Control Point - Defend (100xp)
            16 => ScrimActionType.PointDefend,              // Control Point - Attack (100xp)
            272 => ScrimActionType.ConvertCapturePoint,     // Convert Capture Point (25xp)
            556 => ScrimActionType.ObjectiveDefensePulse,   // Objective Pulse Defend (50xp)
            557 => ScrimActionType.ObjectiveCapturePulse,   // Objective Pulse Capture (100xp)
            _ => ScrimActionType.Unknown
        };
    }
}
