using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Interfaces;

public interface IScrimRulesetManager
{
    Ruleset.Models.Ruleset? ActiveRuleset { get; }

    Task<Ruleset.Models.Ruleset?> GetActiveRulesetAsync(bool forceRefresh = false);
    Task<Ruleset.Models.Ruleset?> GetDefaultRulesetAsync();

    Task<Ruleset.Models.Ruleset?> ActivateRulesetAsync(int rulesetId);
    Task SetUpActiveRulesetAsync();

    Task SeedDefaultRuleset();
    Task SeedScrimActionModels();
    Task<IEnumerable<Ruleset.Models.Ruleset>> GetRulesetsAsync(CancellationToken cancellationToken);
    Task<Ruleset.Models.Ruleset?> ActivateDefaultRulesetAsync();
}
