using System.Threading;
using System.Threading.Tasks;
using DbgCensus.EventStream.Abstractions.Objects.Events.Characters;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Interfaces;

public interface IScrimMatchScorer
{
    Task<ScrimEventScoringResult> ScoreDeathEventAsync(ScrimDeathActionEvent death, CancellationToken ct = default);
    Task<ScrimEventScoringResult> ScoreVehicleDestructionEventAsync(ScrimVehicleDestructionActionEvent destruction, CancellationToken ct = default);
    ScrimEventScoringResult ScoreFacilityControlEvent(ScrimFacilityControlActionEvent control);
    void HandlePlayerLogin(IPlayerLogin login);
    void HandlePlayerLogout(IPlayerLogout login);
    Task SetActiveRulesetAsync(CancellationToken ct = default);
    Task<ScrimEventScoringResult> ScoreReviveEventAsync(ScrimReviveActionEvent revive, CancellationToken ct = default);
    Task<ScrimEventScoringResult> ScoreAssistEventAsync(ScrimAssistActionEvent assist, CancellationToken ct = default);
    Task<ScrimEventScoringResult> ScoreObjectiveTickEventAsync(ScrimObjectiveTickActionEvent objective, CancellationToken ct = default);
}
