using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models;
using squittal.ScrimPlanetmans.App.Models.Forms;
using squittal.ScrimPlanetmans.App.Models.Planetside;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.Abstractions.Services.Rulesets;

public interface IRulesetDataService
{
    int DefaultRulesetId { get; }
    int ActiveRulesetId { get; }
    int CustomDefaultRulesetId { get; }

    Task RefreshRulesetsAsync(CancellationToken ct = default);

    Task<PaginatedList<Ruleset>> GetRulesetListAsync(int? pageIndex, CancellationToken ct);
    Task<Ruleset?> GetRulesetFromIdAsync(int rulesetId, CancellationToken cancellationToken, bool includeCollections = true, bool includeOverlayConfiguration = true);
    Task<IEnumerable<Ruleset>> GetAllRulesetsAsync(CancellationToken cancellationToken);

    Task<IEnumerable<RulesetActionRule>> GetRulesetActionRulesAsync(int rulesetId, CancellationToken ct);
    Task<RulesetOverlayConfiguration?> GetRulesetOverlayConfigurationAsync(int rulesetId, CancellationToken ct);
    Task<IEnumerable<RulesetItemCategoryRule>> GetRulesetItemCategoryRulesAsync(int rulesetId, CancellationToken ct);
    Task<IEnumerable<RulesetItemRule>> GetRulesetItemRulesAsync(int rulesetId, CancellationToken ct);
    Task<IEnumerable<RulesetFacilityRule>> GetRulesetFacilityRulesAsync(int rulesetId, CancellationToken ct);
    Task<IEnumerable<RulesetFacilityRule>> GetUnusedRulesetFacilityRulesAsync(int rulesetId, CancellationToken ct);

    Task<Ruleset?> GetRulesetWithFacilityRules(int rulesetId, CancellationToken ct);
    Task<IEnumerable<RulesetItemCategoryRule>> GetRulesetItemCategoryRulesDeferringToItemRules(int rulesetId, CancellationToken ct);
    Task<IEnumerable<ItemCategory>?> GetItemCategoriesDeferringToItemRulesAsync
    (
        int rulesetId,
        CancellationToken ct = default
    );

    Task<Ruleset?> SaveNewRulesetAsync(Ruleset ruleset, CancellationToken ct = default);
    Task<bool> UpdateRulesetInfo(Ruleset rulesetUpdate, CancellationToken ct);
    Task<bool> SaveRulesetOverlayConfiguration(int rulesetId, RulesetOverlayConfiguration rulesetOverlayConfiguration, CancellationToken ct);
    Task SaveRulesetActionRules(int rulesetId, IEnumerable<RulesetActionRule> rules, CancellationToken ct = default);
    Task SaveRulesetItemCategoryRules(int rulesetId, IEnumerable<RulesetItemCategoryRule> rules, CancellationToken ct = default);
    Task SaveRulesetItemRules(int rulesetId, IEnumerable<RulesetItemRule> rules, CancellationToken ct = default);
    Task SaveRulesetFacilityRulesAsync(int rulesetId, IEnumerable<RulesetFacilityRuleChange> rules, CancellationToken ct = default);

    void SetActiveRulesetId(int rulesetId);
    Task<Ruleset?> SetCustomDefaultRulesetAsync(int rulesetId, CancellationToken ct = default);

    Task<bool> CanDeleteRuleset(int rulesetId, CancellationToken cancellationToken);
    Task<bool> HasRulesetBeenUsedAsync(int rulesetId, CancellationToken ct);
    Task<bool> DeleteRulesetAsync(int rulesetId, CancellationToken ct = default);

    Task<bool> ExportRulesetToJsonFile(int rulesetId, CancellationToken ct);

    Task<Ruleset?> ImportNewRulesetFromJsonFile
    (
        string fileName,
        bool returnCollections = false,
        bool returnOverlayConfiguration = false,
        CancellationToken ct = default
    );

    IEnumerable<string> GetJsonRulesetFileNames();
}
