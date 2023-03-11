using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MRuleset = squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models.Ruleset;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Interfaces;

public interface IScrimRulesetManager
{
    MRuleset? ActiveRuleset { get; }

    Task<IEnumerable<MRuleset>> GetRulesetsAsync(CancellationToken ct = default);
    Task<MRuleset?> GetActiveRulesetAsync(bool forceRefresh = false, CancellationToken ct = default);
    Task<MRuleset?> GetDefaultRulesetAsync(CancellationToken ct = default);

    Task<bool> ActivateRulesetAsync(int rulesetId, CancellationToken ct = default);
    Task<bool> ActivateDefaultRulesetAsync(CancellationToken ct = default);

    Task SetUpActiveRulesetAsync(CancellationToken ct = default);
    Task SeedDefaultRulesetAsync(CancellationToken ct = default);
    Task SeedScrimActionModelsAsync(CancellationToken ct = default);
}
