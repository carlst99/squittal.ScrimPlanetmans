using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models.Planetside.Events;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Interfaces;

public interface IScrimMatchScorer
{
    Task<ScrimEventScoringResult> ScoreDeathEvent(ScrimDeathActionEvent death);
    Task<ScrimEventScoringResult> ScoreVehicleDestructionEvent(ScrimVehicleDestructionActionEvent destruction);
    ScrimEventScoringResult ScoreFacilityControlEvent(ScrimFacilityControlActionEvent control);
    void HandlePlayerLogin(PlayerLogin login);
    void HandlePlayerLogout(PlayerLogout login);
    Task SetActiveRulesetAsync();
    Task<ScrimEventScoringResult> ScoreReviveEvent(ScrimReviveActionEvent revive);
    Task<ScrimEventScoringResult> ScoreAssistEvent(ScrimAssistActionEvent assist);
    Task<ScrimEventScoringResult> ScoreObjectiveTickEvent(ScrimObjectiveTickActionEvent objective);
}
