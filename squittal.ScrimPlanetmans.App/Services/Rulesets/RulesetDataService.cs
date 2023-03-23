using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusRest;
using squittal.ScrimPlanetmans.App.Abstractions.Services.Planetside;
using squittal.ScrimPlanetmans.App.Abstractions.Services.Rulesets;
using squittal.ScrimPlanetmans.App.Data;
using squittal.ScrimPlanetmans.App.Data.Interfaces;
using squittal.ScrimPlanetmans.App.Models;
using squittal.ScrimPlanetmans.App.Models.CensusRest;
using squittal.ScrimPlanetmans.App.Models.Forms;
using squittal.ScrimPlanetmans.App.Models.Planetside;
using squittal.ScrimPlanetmans.App.ScrimMatch.Events;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;
using squittal.ScrimPlanetmans.App.Services.ScrimMatch.Interfaces;

namespace squittal.ScrimPlanetmans.App.Services.Rulesets;

public partial class RulesetDataService : IRulesetDataService
{
    private const int RULESET_BROWSER_PAGE_SIZE = 15;

    private static readonly Regex _rulesetNameRegex = GetRulesetNameRegex();
    private static readonly Regex _rulesetDefaultMatchTitleRegex = _rulesetNameRegex; //(^(?!.)$|^([A-Za-z0-9()\[\]\-_'.][ ]{0,1}){1,49}[A-Za-z0-9()\[\]\-_'.]$)

    private readonly ILogger<RulesetDataService> _logger;
    private readonly IDbContextHelper _dbContextHelper;
    private readonly ICensusItemService _itemService;
    private readonly ICensusMapRegionService _mapRegionService;
    private readonly IItemCategoryService _itemCategoryService;
    private readonly IScrimMessageBroadcastService _messageService;
    private readonly IRulesetFileService _rulesetFileService;

    private readonly ConcurrentDictionary<int, Ruleset> _rulesetsMap = new();
    private readonly KeyedSemaphoreSlim _rulesetLock = new();
    private readonly KeyedSemaphoreSlim _overlayConfigurationLock = new();
    private readonly KeyedSemaphoreSlim _actionRulesLock = new();
    private readonly KeyedSemaphoreSlim _itemCategoryRulesLock = new();
    private readonly KeyedSemaphoreSlim _itemRulesLock = new();
    private readonly KeyedSemaphoreSlim _facilityRulesLock = new();
    private readonly KeyedSemaphoreSlim _rulesetExportLock = new();
    private readonly AutoResetEvent _defaultRulesetAutoEvent = new(true);

    public int ActiveRulesetId { get; private set; }
    public int CustomDefaultRulesetId { get; private set; }
    public int DefaultRulesetId => 1;

    public RulesetDataService
    (
        ILogger<RulesetDataService> logger,
        IDbContextHelper dbContextHelper,
        ICensusItemService itemService,
        ICensusMapRegionService mapRegionService,
        IItemCategoryService itemCategoryService,
        IScrimMessageBroadcastService messageService,
        IRulesetFileService rulesetFileService
    )
    {
        _logger = logger;
        _dbContextHelper = dbContextHelper;
        _mapRegionService = mapRegionService;
        _itemService = itemService;
        _itemCategoryService = itemCategoryService;
        _messageService = messageService;
        _rulesetFileService = rulesetFileService;
    }

    public async Task RefreshRulesetsAsync(CancellationToken ct = default)
    {
        await RefreshItemCategoriesAsync(ct);
        await RefreshItemsAsync(ct);
    }

    private async Task RefreshItemCategoriesAsync(CancellationToken ct)
    {
        _logger.LogInformation("Updating rulesets post-Item Category Store Refresh");

            IEnumerable<uint>? getWeaponItemCategoryIds = await _itemCategoryService.GetWeaponItemCategoryIdsAsync
            (
                ct
            );
            if (getWeaponItemCategoryIds is null)
                return;

            uint[] weaponItemCategoryIds = getWeaponItemCategoryIds.ToArray();

            Ruleset[] storeRulesets = (await GetAllRulesetsAsync(ct)).ToArray();

            if (storeRulesets.Length is 0)
            {
                _logger.LogInformation
                (
                    "Finished updating rulesets post-Item Category Store Refresh. No store rulesets found"
                );

                return;
            }

            foreach (Ruleset ruleset in storeRulesets)
            {
                IEnumerable<RulesetItemCategoryRule> rulesetItemCategoryRules
                    = await GetRulesetItemCategoryRulesAsync(ruleset.Id, ct);

                uint[] missingItemCategoryIds = weaponItemCategoryIds
                    .Where(id => rulesetItemCategoryRules.All(rule => rule.ItemCategoryId != id))
                    .ToArray();

                if (missingItemCategoryIds.Length is 0)
                    continue;

                RulesetItemCategoryRule[] newRules = missingItemCategoryIds.Select
                    (
                        id => BuildRulesetItemCategoryRule(ruleset.Id, id)
                    )
                    .ToArray();
                await SaveRulesetItemCategoryRules(ruleset.Id, newRules, ct);

                _logger.LogInformation
                (
                    "Updated Item Category Rules for Ruleset {ID} post-Item Category Store Refresh. " +
                    "New Rules: {Count}",
                    ruleset.Id,
                    newRules.Length
                );
            }

        _logger.LogInformation("Finished updating rulesets post-Item Category Store Refresh");
    }

    private async Task RefreshItemsAsync(CancellationToken ct)
    {
        _logger.LogInformation("Updating rulesets post-Item Store Refresh");

            IReadOnlyList<CensusItem>? allWeaponItems = await _itemService.GetAllWeaponsAsync(ct);
            if (allWeaponItems is null)
                return;

            Ruleset[] storeRulesets = (await GetAllRulesetsAsync(ct)).ToArray();

            if (storeRulesets.Length is 0)
            {
                _logger.LogInformation
                (
                    "Finished updating rulesets post-Item Store Refresh. No store rulesets found"
                );
                return;
            }

            foreach (Ruleset ruleset in storeRulesets)
            {
                IEnumerable<ItemCategory>? getDeferredItemCategoryRules
                    = await GetItemCategoriesDeferringToItemRulesAsync(ruleset.Id, ct);

                if (getDeferredItemCategoryRules is null)
                    continue;
                ItemCategory[] deferredItemCategoryRules = getDeferredItemCategoryRules.ToArray();

                if (!deferredItemCategoryRules.Any())
                    continue;

                IEnumerable<RulesetItemRule> rulesetItemRules = await GetRulesetItemRulesAsync
                (
                    ruleset.Id,
                    ct
                );
                HashSet<uint> rulesetItems = new(rulesetItemRules.Select(x => x.ItemId));

                List<CensusItem> missingItemIds = allWeaponItems.Where
                    (
                        w => deferredItemCategoryRules.Any(rule => rule.Id == w.ItemCategoryId)
                            && !rulesetItems.Contains(w.ItemId)
                    )
                    .ToList();

                if (!missingItemIds.Any())
                    continue;

                RulesetItemRule[] newRules = missingItemIds.Select
                    (
                        w => BuildRulesetItemRule(ruleset.Id, w.ItemId, w.ItemCategoryId)
                    )
                    .ToArray();
                await SaveRulesetItemRules(ruleset.Id, newRules, ct);

                _logger.LogInformation
                (
                    "Updated Item Rules for Ruleset {ID} post-Item Category Store Refresh. " +
                    "New Rules: {Count}",
                    ruleset.Id,
                    newRules.Length
                );
            }

        _logger.LogInformation("Finished updating rulesets post-Item Store Refresh");
    }

    #region GET methods

    public async Task<PaginatedList<Ruleset>> GetRulesetListAsync(int? pageIndex, CancellationToken cancellationToken)
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        IOrderedQueryable<Ruleset> rulesetsQuery = dbContext.Rulesets
            .OrderByDescending(r => r.Id == ActiveRulesetId)
            .ThenByDescending(r => r.IsCustomDefault)
            .ThenByDescending(r => r.IsDefault)
            .ThenBy(r => r.Name);

        PaginatedList<Ruleset> paginatedList = await PaginatedList<Ruleset>.CreateAsync
        (
            rulesetsQuery,
            pageIndex ?? 1,
            RULESET_BROWSER_PAGE_SIZE,
            cancellationToken
        );

        return paginatedList;
    }

    public async Task<IEnumerable<Ruleset>> GetAllRulesetsAsync(CancellationToken cancellationToken)
    {
        if (_rulesetsMap.IsEmpty)
            await SetUpRulesetsMapAsync(cancellationToken);

        if (!_rulesetsMap.Any())
            return Array.Empty<Ruleset>();

        return _rulesetsMap.Values.ToList();
    }

    public async Task<Ruleset?> GetRulesetFromIdAsync
    (
        int rulesetId,
        CancellationToken cancellationToken,
        bool includeCollections = true,
        bool includeOverlayConfiguration = true
    )
    {
        try
        {
            if (_rulesetsMap.IsEmpty)
                await SetUpRulesetsMapAsync(cancellationToken);

            if (_rulesetsMap.IsEmpty)
                return null;

            if (!_rulesetsMap.TryGetValue(rulesetId, out Ruleset? ruleset))
                return null;

            if (includeCollections)
            {
                Task<IEnumerable<RulesetActionRule>> actionRulesTask = GetRulesetActionRulesAsync(rulesetId, cancellationToken);
                Task<IEnumerable<RulesetItemCategoryRule>> itemCategoryRulesTask = GetRulesetItemCategoryRulesAsync(rulesetId, cancellationToken);
                Task<IEnumerable<RulesetItemRule>> itemRulesTask = GetRulesetItemRulesAsync(rulesetId, cancellationToken);
                Task<IEnumerable<RulesetFacilityRule>> facilityRulesTask = GetRulesetFacilityRulesAsync(rulesetId, cancellationToken);
                await Task.WhenAll(actionRulesTask, itemCategoryRulesTask, itemRulesTask, facilityRulesTask);

                ruleset.RulesetActionRules = (await actionRulesTask).ToList();
                ruleset.RulesetItemCategoryRules = (await itemCategoryRulesTask).ToList();
                ruleset.RulesetItemRules = (await itemRulesTask).ToList();
                ruleset.RulesetFacilityRules = (await facilityRulesTask).ToList();
            }

            if (includeOverlayConfiguration)
            {
                ruleset.RulesetOverlayConfiguration = await GetRulesetOverlayConfigurationAsync(rulesetId, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            return ruleset;
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("Task Request cancelled: GetRulesetFromIdAsync rulesetId {ID}", rulesetId);
            return null;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Request cancelled: GetRulesetFromIdAsync rulesetId {ID}", rulesetId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get ruleset by ID");

            return null;
        }
    }

    public Task<Ruleset?> GetRulesetWithFacilityRules(int rulesetId, CancellationToken cancellationToken)
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        Ruleset? ruleset =  dbContext.Rulesets
            .Where(r => r.Id == rulesetId)
            .Include("RulesetFacilityRules")
            .Include("RulesetFacilityRules.MapRegion")
            .FirstOrDefault();

        return Task.FromResult(ruleset);
    }

    public Task<RulesetOverlayConfiguration?> GetRulesetOverlayConfigurationAsync(int rulesetId, CancellationToken ct)
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        RulesetOverlayConfiguration? configuration = dbContext.RulesetOverlayConfigurations
            .FirstOrDefault(c => c.RulesetId == rulesetId);

        return Task.FromResult(configuration);
    }

    #region GET Ruleset Rules

    public Task<IEnumerable<RulesetActionRule>> GetRulesetActionRulesAsync(int rulesetId, CancellationToken ct)
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        IEnumerable<RulesetActionRule> result = dbContext.RulesetActionRules
            .Where(r => r.RulesetId == rulesetId);

        return Task.FromResult(result);
    }

    public Task<IEnumerable<RulesetItemCategoryRule>> GetRulesetItemCategoryRulesAsync
    (
        int rulesetId,
        CancellationToken ct
    )
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        IEnumerable<RulesetItemCategoryRule> result = dbContext.RulesetItemCategoryRules
            .Where(r => r.RulesetId == rulesetId);

        return Task.FromResult(result);
    }

    public Task<IEnumerable<RulesetItemRule>> GetRulesetItemRulesAsync(int rulesetId, CancellationToken ct)
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        IEnumerable<RulesetItemRule> result =  dbContext.RulesetItemRules
            .Where(r => r.RulesetId == rulesetId);

        return Task.FromResult(result);
    }

    public IEnumerable<RulesetItemRule> GetRulesetItemRulesForItemCategoryId
    (
        int rulesetId,
        uint itemCategoryId
    )
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        return dbContext.RulesetItemRules
            .Where(r => r.RulesetId == rulesetId && r.ItemCategoryId == itemCategoryId);
    }

    public Task<IEnumerable<RulesetFacilityRule>> GetRulesetFacilityRulesAsync(int rulesetId, CancellationToken ct)
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        IEnumerable<RulesetFacilityRule> result = dbContext.RulesetFacilityRules
            .Where(r => r.RulesetId == rulesetId);

        return Task.FromResult(result);
    }

    public async Task<IEnumerable<RulesetFacilityRule>> GetUnusedRulesetFacilityRulesAsync(int rulesetId, CancellationToken ct)
    {
        IEnumerable<RulesetFacilityRule> usedFacilities = await GetRulesetFacilityRulesAsync(rulesetId, ct);

        IEnumerable<CensusMapRegion>? allFacilities = await _mapRegionService.GetAllAsync(ct);
        if (allFacilities is null)
            return Array.Empty<RulesetFacilityRule>();

        return allFacilities.Where(a => usedFacilities.All(u => u.FacilityId != a.FacilityId))
            .OrderBy(mr => mr.ZoneId)
            .ThenBy(mr => mr.FacilityName)
            .Select(mr => ConvertToFacilityRule(rulesetId, mr));
    }

    private static RulesetFacilityRule ConvertToFacilityRule(int rulesetId, CensusMapRegion mapRegion)
    {
        return new RulesetFacilityRule
        {
            RulesetId = rulesetId,
            FacilityId = mapRegion.FacilityId
        };
    }

    public async Task<IEnumerable<ItemCategory>?> GetItemCategoriesDeferringToItemRulesAsync
    (
        int rulesetId,
        CancellationToken cancellationToken
    )
    {
        IEnumerable<uint> categoryIds = GetItemCategoryIdsDeferringToItemRulesAsync(rulesetId);

        return await _itemCategoryService.GetItemCategoriesFromIdsAsync(categoryIds, cancellationToken);
    }

    private IEnumerable<uint> GetItemCategoryIdsDeferringToItemRulesAsync(int rulesetId)
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        return dbContext.RulesetItemCategoryRules
            .Where(r => r.RulesetId == rulesetId && r.DeferToItemRules)
            .Select(r => r.ItemCategoryId);
    }

    public Task<IEnumerable<RulesetItemCategoryRule>> GetRulesetItemCategoryRulesDeferringToItemRules
    (
        int rulesetId,
        CancellationToken ct
    )
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        IEnumerable<RulesetItemCategoryRule> rules = dbContext.RulesetItemCategoryRules
            .Where(r => r.RulesetId == rulesetId && r.DeferToItemRules)
            .Include("ItemCategory");

        return Task.FromResult(rules);
    }

    #endregion GET Ruleset Rules

    #endregion GET methods

    #region SAVE / UPDATE methods

    public async Task<bool> UpdateRulesetInfo(Ruleset rulesetUpdate, CancellationToken ct)
    {
        int updateId = rulesetUpdate.Id;

        if (updateId == DefaultRulesetId || rulesetUpdate.IsDefault)
        {
            return false;
        }

        string updateName = rulesetUpdate.Name;
        int updateRoundLength = rulesetUpdate.DefaultRoundLength;
        string updateMatchTitle = rulesetUpdate.DefaultMatchTitle;
        bool updateEndRoundOnFacilityCapture = rulesetUpdate.DefaultEndRoundOnFacilityCapture;

        Ruleset oldRuleset = new();

        if (!IsValidRulesetName(updateName))
        {
            _logger.LogError("Error updating Ruleset {ID} info: invalid ruleset name", updateId);
            return false;
        }

        if (!IsValidRulesetDefaultRoundLength(updateRoundLength))
        {
            _logger.LogError("Error updating Ruleset {ID} info: invalid default round length", updateId);
            return false;
        }

        if (!IsValidRulesetDefaultMatchTitle(updateMatchTitle))
        {
            _logger.LogError("Error updating Ruleset {ID} info: invalid default match title", updateId);
        }

        using (await _rulesetLock.WaitAsync($"{updateId}", ct))
        {
            try
            {
                using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
                PlanetmansDbContext dbContext = factory.GetDbContext();

                Ruleset? storeEntity = await GetRulesetFromIdAsync(updateId, ct, false, false);

                if (storeEntity == null)
                {
                    return false;
                }

                oldRuleset.Name = storeEntity.Name;
                oldRuleset.DefaultRoundLength = storeEntity.DefaultRoundLength;
                oldRuleset.DefaultMatchTitle = storeEntity.DefaultMatchTitle;
                oldRuleset.DefaultEndRoundOnFacilityCapture = storeEntity.DefaultEndRoundOnFacilityCapture;

                storeEntity.Name = updateName;
                storeEntity.DefaultRoundLength = updateRoundLength;
                storeEntity.DefaultMatchTitle = updateMatchTitle;
                storeEntity.DefaultEndRoundOnFacilityCapture = updateEndRoundOnFacilityCapture;

                storeEntity.DateLastModified = DateTime.UtcNow;

                dbContext.Rulesets.Update(storeEntity);

                await dbContext.SaveChangesAsync(ct);

                await SetUpRulesetsMapAsync(ct);

                RulesetSettingChangeMessage changeMessage = new(storeEntity, oldRuleset);
                _messageService.BroadcastRulesetSettingChangeMessage(changeMessage);

                _logger.LogInformation("{ChangeMessage}", changeMessage.Info);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Ruleset {ID} info", updateId);
                return false;
            }
        }
    }

    /*
     * Upsert New or Modified Overlay Configuration for a specific ruleset
     */
    public async Task<bool> SaveRulesetOverlayConfiguration(int rulesetId, RulesetOverlayConfiguration rulesetOverlayConfiguration, CancellationToken ct)
    {
        if (rulesetId == DefaultRulesetId)
        {
            return false;
        }

        using (await _overlayConfigurationLock.WaitAsync($"{rulesetId}", ct))
        {
            try
            {
                using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
                PlanetmansDbContext dbContext = factory.GetDbContext();

                RulesetOverlayConfiguration? storeConfiguration = await dbContext.RulesetOverlayConfigurations.Where(c => c.RulesetId == rulesetId).FirstOrDefaultAsync(ct);

                ct.ThrowIfCancellationRequested();

                RulesetOverlayConfiguration? previousConfiguration = storeConfiguration == null ? null : BuildRulesetOverlayConfiguration(rulesetId, storeConfiguration.UseCompactLayout, storeConfiguration.StatsDisplayType, storeConfiguration.ShowStatusPanelScores);

                RulesetOverlayConfiguration newConfiguration = BuildRulesetOverlayConfiguration(rulesetId, rulesetOverlayConfiguration.UseCompactLayout, rulesetOverlayConfiguration.StatsDisplayType, rulesetOverlayConfiguration.ShowStatusPanelScores);


                if (storeConfiguration == null)
                {
                    storeConfiguration = rulesetOverlayConfiguration;
                    dbContext.RulesetOverlayConfigurations.Add(storeConfiguration);
                }
                else
                {
                    storeConfiguration = rulesetOverlayConfiguration;
                    dbContext.RulesetOverlayConfigurations.Update(storeConfiguration);
                }
                Ruleset? storeRuleset = await GetRulesetFromIdAsync(rulesetId, ct, false, false);
                if (storeRuleset != null)
                {
                    storeRuleset.DateLastModified = DateTime.UtcNow;
                    dbContext.Rulesets.Update(storeRuleset);
                }

                await dbContext.SaveChangesAsync(ct);

                if (storeRuleset is not null && previousConfiguration is not null)
                {
                    // TODO: Is this going to create issues because we're not sending updates going from no config to new config?
                    RulesetOverlayConfigurationChangeMessage message = new(storeRuleset, newConfiguration, previousConfiguration);
                    _messageService.BroadcastRulesetOverlayConfigurationChangeMessage(message);
                }

                _logger.LogInformation("Saved Overlay Configuration updates for Ruleset {ID}", rulesetId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving RulesetOverlayConfiguration changes to database");

                return false;
            }
        }
    }

    #region Save Ruleset Rules

    /*
     * Upsert New or Modified RulesetActionRules for a specific ruleset.
     */
    public async Task SaveRulesetActionRules
    (
        int rulesetId,
        IEnumerable<RulesetActionRule> rules,
        CancellationToken ct = default
    )
    {
        if (rulesetId == DefaultRulesetId)
        {
            return;
        }

        using (await _actionRulesLock.WaitAsync($"{rulesetId}", ct))
        {
            List<RulesetActionRule> ruleUpdates = rules.Where(rule => rule.RulesetId == rulesetId).ToList();

            if (!ruleUpdates.Any())
            {
                return;
            }

            try
            {
                using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
                PlanetmansDbContext dbContext = factory.GetDbContext();

                List<RulesetActionRule> storeRules = await dbContext.RulesetActionRules
                    .Where(rule => rule.RulesetId == rulesetId)
                    .ToListAsync(cancellationToken: ct);

                List<RulesetActionRule> newEntities = new();

                foreach (RulesetActionRule rule in ruleUpdates)
                {
                    RulesetActionRule? storeEntity = storeRules.FirstOrDefault(r => r.ScrimActionType == rule.ScrimActionType);

                    if (storeEntity == null)
                    {
                        newEntities.Add(rule);
                    }
                    else
                    {
                        storeEntity = rule;
                        dbContext.RulesetActionRules.Update(storeEntity);
                    }
                }

                if (newEntities.Any())
                {
                    dbContext.RulesetActionRules.AddRange(newEntities);
                }

                Ruleset? storeRuleset = await GetRulesetFromIdAsync(rulesetId, ct, false, false);
                if (storeRuleset != null)
                {
                    storeRuleset.DateLastModified = DateTime.UtcNow;
                    dbContext.Rulesets.Update(storeRuleset);
                }

                await dbContext.SaveChangesAsync(ct);

                if (storeRuleset is not null)
                {
                    RulesetRuleChangeMessage message = new(storeRuleset, RulesetRuleChangeType.ActionRule);
                    _messageService.BroadcastRulesetRuleChangeMessage(message);
                }

                _logger.LogInformation("Saved Action Rule updates for Ruleset {ID}", rulesetId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving RulesetActionRule changes to database");
            }
        }
    }

    /*
     * Upsert New or Modified RulesetItemCategoryRules for a specific ruleset.
     */
    public async Task SaveRulesetItemCategoryRules
    (
        int rulesetId,
        IEnumerable<RulesetItemCategoryRule> rules,
        CancellationToken ct = default
    )
    {
        if (rulesetId == DefaultRulesetId)
        {
            return;
        }

        using (await _itemCategoryRulesLock.WaitAsync($"{rulesetId}", ct))
        {
            List<RulesetItemCategoryRule> ruleUpdates = rules.Where(rule => rule.RulesetId == rulesetId).ToList();

            if (!ruleUpdates.Any())
            {
                return;
            }

            try
            {
                using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
                PlanetmansDbContext dbContext = factory.GetDbContext();

                List<RulesetItemCategoryRule> newEntities = new();
                RulesetItemCategoryRule[] storeRules = (await GetRulesetItemCategoryRulesAsync(rulesetId, ct))
                    .ToArray();

                foreach (RulesetItemCategoryRule rule in ruleUpdates)
                {
                    // Only allow deferring to either Planetside Class Settings or Item Rules. Give Preference to Item Rules
                    if (rule is { DeferToItemRules: true, DeferToPlanetsideClassSettings: true })
                    {
                        rule.DeferToPlanetsideClassSettings = false;
                    }

                    RulesetItemCategoryRule? storeEntity = storeRules.FirstOrDefault(r => r.ItemCategoryId == rule.ItemCategoryId);

                    if (storeEntity == null)
                    {
                        newEntities.Add(rule);
                    }
                    else
                    {
                        storeEntity = rule;
                        dbContext.RulesetItemCategoryRules.Update(storeEntity);
                    }
                }

                if (newEntities.Any())
                {
                    dbContext.RulesetItemCategoryRules.AddRange(newEntities);
                }

                Ruleset? storeRuleset = await dbContext.Rulesets.Where(r => r.Id == rulesetId)
                    .FirstOrDefaultAsync(cancellationToken: ct);

                if (storeRuleset != null)
                {
                    storeRuleset.DateLastModified = DateTime.UtcNow;
                    dbContext.Rulesets.Update(storeRuleset);
                }


                List<Task> TaskList = new();

                foreach (RulesetItemCategoryRule rule in dbContext.RulesetItemCategoryRules)
                {
                    Task itemRulesTask = UpdateItemRulesForItemCategoryRule(rule);
                    TaskList.Add(itemRulesTask);
                }

                await Task.WhenAll(TaskList);

                await dbContext.SaveChangesAsync(ct);

                if (storeRuleset is not null)
                {
                    RulesetRuleChangeMessage message = new(storeRuleset, RulesetRuleChangeType.ItemCategoryRule);
                    _messageService.BroadcastRulesetRuleChangeMessage(message);
                }

                _logger.LogInformation("Saved Item Category Rule updates for Ruleset {ID}", rulesetId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving RulesetItemCategoryRule changes to database");
            }
        }
    }

    /*
     * Upsert New or Modified RulesetItemRules for a specific ruleset.
     */
    public async Task SaveRulesetItemRules
    (
        int rulesetId,
        IEnumerable<RulesetItemRule> rules,
        CancellationToken ct = default
    )
    {
        if (rulesetId == DefaultRulesetId)
        {
            return;
        }

        using (await _itemRulesLock.WaitAsync($"{rulesetId}", ct))
        {
            List<RulesetItemRule> ruleUpdates = rules.Where(rule => rule.RulesetId == rulesetId).ToList();

            if (!ruleUpdates.Any())
            {
                return;
            }

            try
            {
                using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
                PlanetmansDbContext dbContext = factory.GetDbContext();

                RulesetItemRule[] storeRules = (await GetRulesetItemRulesAsync(rulesetId, ct)).ToArray();
                List<RulesetItemRule> newEntities = new();

                foreach (RulesetItemRule rule in ruleUpdates)
                {
                    RulesetItemRule? storeEntity = storeRules.FirstOrDefault(r => r.ItemId == rule.ItemId);

                    if (storeEntity == null)
                    {
                        newEntities.Add(rule);
                    }
                    else
                    {
                        storeEntity = rule;
                        dbContext.RulesetItemRules.Update(storeEntity);
                    }
                }

                if (newEntities.Any())
                {
                    dbContext.RulesetItemRules.AddRange(newEntities);
                }

                Ruleset? storeRuleset = await dbContext.Rulesets.Where(r => r.Id == rulesetId)
                    .FirstOrDefaultAsync(cancellationToken: ct);

                if (storeRuleset != null)
                {
                    storeRuleset.DateLastModified = DateTime.UtcNow;
                    dbContext.Rulesets.Update(storeRuleset);
                }

                await dbContext.SaveChangesAsync(ct);

                if (storeRuleset is not null)
                {
                    RulesetRuleChangeMessage message = new(storeRuleset, RulesetRuleChangeType.ItemRule);
                    _messageService.BroadcastRulesetRuleChangeMessage(message);
                }

                _logger.LogInformation("Saved Item Rule updates for Ruleset {ID}", rulesetId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving RulesetItemRule changes to database");
            }
        }
    }

    /*
     * Upsert New or Modified RulesetFacilityRules for a specific ruleset.
     */
    public async Task SaveRulesetFacilityRules(int rulesetId, IEnumerable<RulesetFacilityRuleChange> rules)
    {
        if (rulesetId == DefaultRulesetId)
        {
            return;
        }

        using (await _facilityRulesLock.WaitAsync($"{rulesetId}"))
        {
            List<RulesetFacilityRuleChange> ruleUpdates = rules.Where(rule => rule.RulesetFacilityRule.RulesetId == rulesetId).ToList();

            if (!ruleUpdates.Any())
            {
                return;
            }

            try
            {
                using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
                PlanetmansDbContext dbContext = factory.GetDbContext();

                List<RulesetFacilityRule> storeRules = await dbContext.RulesetFacilityRules.Where(rule => rule.RulesetId == rulesetId).ToListAsync();

                List<RulesetFacilityRule> newEntities = new();

                foreach (RulesetFacilityRuleChange rule in ruleUpdates)
                {
                    RulesetFacilityRule? storeEntity = storeRules.FirstOrDefault(r => r.FacilityId == rule.RulesetFacilityRule.FacilityId);

                    if (storeEntity == null)
                    {
                        if (rule.ChangeType == RulesetFacilityRuleChangeType.Add)
                            newEntities.Add(rule.RulesetFacilityRule);
                    }
                    else
                    {
                        if (rule.ChangeType == RulesetFacilityRuleChangeType.Add)
                        {
                            storeEntity = rule.RulesetFacilityRule;
                            dbContext.RulesetFacilityRules.Update(storeEntity);
                        }
                        else if (rule.ChangeType == RulesetFacilityRuleChangeType.Remove)
                        {
                            dbContext.RulesetFacilityRules.Remove(storeEntity);
                        }
                    }
                }

                if (newEntities.Any())
                {
                    dbContext.RulesetFacilityRules.AddRange(newEntities);
                }

                Ruleset? storeRuleset = await dbContext.Rulesets.Where(r => r.Id == rulesetId).FirstOrDefaultAsync();
                if (storeRuleset != null)
                {
                    storeRuleset.DateLastModified = DateTime.UtcNow;
                    dbContext.Rulesets.Update(storeRuleset);
                }

                await dbContext.SaveChangesAsync();

                if (storeRuleset is not null)
                {
                    RulesetRuleChangeMessage message = new(storeRuleset, RulesetRuleChangeType.FacilityRule);
                    _messageService.BroadcastRulesetRuleChangeMessage(message);
                }

                _logger.LogInformation("Saved Facility Rule updates for Ruleset {ID}", rulesetId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving RulesetFacilityRule changes to database");
            }
        }
    }

    #endregion Save Ruleset Rules

    public async Task<Ruleset?> SaveNewRulesetAsync(Ruleset ruleset, CancellationToken ct = default)
    {
        Ruleset? created = await CreateRulesetAsync(ruleset, ct);

        if (created == null)
            return null;

        List<Task> TaskList = new();

        Task overlayConfigurationTask = SeedNewRulesetDefaultOverlayConfiguration(created.Id);
        TaskList.Add(overlayConfigurationTask);

        Task itemCategoryRulesTask = SeedNewRulesetDefaultItemCategoryRules(created.Id);
        TaskList.Add(itemCategoryRulesTask);

        Task itemRulesTask = SeedNewRulesetDefaultItemRules(created.Id);
        TaskList.Add(itemRulesTask);

        Task actionRulesTask = SeedNewRulesetDefaultActionRules(created.Id);
        TaskList.Add(actionRulesTask);

        Task facilityRulesTask = SeedNewRulesetDefaultFacilityRules(created.Id);
        TaskList.Add(facilityRulesTask);

        await Task.WhenAll(TaskList);

        return created;
    }

    private async Task<Ruleset?> CreateRulesetAsync(Ruleset ruleset, CancellationToken ct)
    {
        if (!IsValidRulesetName(ruleset.Name) || ruleset.IsDefault
            || !IsValidRulesetDefaultRoundLength(ruleset.DefaultRoundLength) || !IsValidRulesetDefaultMatchTitle(ruleset.DefaultMatchTitle))
        {
            return null;
        }

        using (await _rulesetLock.WaitAsync($"{ruleset.Id}", ct))
        {
            try
            {
                using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
                PlanetmansDbContext dbContext = factory.GetDbContext();

                if (ruleset.DateCreated == default)
                {
                    ruleset.DateCreated = DateTime.UtcNow;
                }

                dbContext.Rulesets.Add(ruleset);

                await dbContext.SaveChangesAsync(ct);

                await SetUpRulesetsMapAsync(ct);

                return ruleset;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create a ruleset");

                return null;
            }
        }
    }

    private RulesetOverlayConfiguration BuildRulesetOverlayConfiguration(int rulesetId, bool useCompactLayout = false, OverlayStatsDisplayType statsDisplayType = OverlayStatsDisplayType.InfantryScores, bool? ShowStatusPanelScores = null)
    {
        return new RulesetOverlayConfiguration
        {
            RulesetId = rulesetId,
            UseCompactLayout = useCompactLayout,
            StatsDisplayType = statsDisplayType,
            ShowStatusPanelScores = ShowStatusPanelScores
        };
    }

    private async Task SeedNewRulesetDefaultOverlayConfiguration(int rulesetId)
    {
        using (await _overlayConfigurationLock.WaitAsync($"{rulesetId}"))
        {
            try
            {
                using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
                PlanetmansDbContext dbContext = factory.GetDbContext();

                int defaultRulesetId = await dbContext.Rulesets.Where(r => r.IsDefault).Select(r => r.Id).FirstOrDefaultAsync();

                RulesetOverlayConfiguration? defaultConfiguration = await dbContext.RulesetOverlayConfigurations.Where(c => c.RulesetId == defaultRulesetId).FirstOrDefaultAsync();

                if (defaultConfiguration == null)
                {
                    _logger.LogWarning($"Failed to seed new Ruleset Overlay Configuration. Default Ruleset Overlay Configuration is null.");

                    return;
                }

                RulesetOverlayConfiguration newConfiguration = BuildRulesetOverlayConfiguration(rulesetId, defaultConfiguration.UseCompactLayout, defaultConfiguration.StatsDisplayType, defaultConfiguration.ShowStatusPanelScores);

                dbContext.RulesetOverlayConfigurations.Add(newConfiguration);

                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed a default overlay configuration for a new ruleset");
            }
        }
    }

    /*
     * Seed default RulesetItemCategoryRules for a newly created ruleset. Will not do anything if the ruleset
     * already has any RulesetItemCategoryRules in the database.
    */
    private async Task SeedNewRulesetDefaultActionRules(int rulesetId)
    {
        try
        {
            using (await _actionRulesLock.WaitAsync($"{rulesetId}"))
            {
                using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
                PlanetmansDbContext dbContext = factory.GetDbContext();

                int storeRulesCount = await dbContext.RulesetActionRules
                    .Where(r => r.RulesetId == rulesetId).CountAsync();

                if (storeRulesCount > 0)
                    return;

                int defaultRulesetId = await dbContext.Rulesets
                    .Where(r => r.IsDefault)
                    .Select(r => r.Id)
                    .FirstOrDefaultAsync();

                List<RulesetActionRule> defaultRules = await dbContext.RulesetActionRules
                    .Where(r => r.RulesetId == defaultRulesetId)
                    .ToListAsync();

                dbContext.RulesetActionRules
                    .AddRange
                    (
                        defaultRules.Select
                        (
                            r => BuildRulesetActionRule(rulesetId, r.ScrimActionType, r.Points, r.DeferToItemCategoryRules)
                        )
                    );

                await dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed new a new ruleset with the default action rules");
        }
    }

    private static RulesetActionRule BuildRulesetActionRule
    (
        int rulesetId,
        ScrimActionType actionType,
        int points = 0,
        bool deferToItemCategoryRules = false
    )
    {
        return new RulesetActionRule
        {
            RulesetId = rulesetId,
            ScrimActionType = actionType,
            Points = points,
            DeferToItemCategoryRules = deferToItemCategoryRules,
            ScrimActionTypeDomain = ScrimAction.GetDomainFromActionType(actionType)
        };
    }

    private async Task SeedNewRulesetDefaultItemCategoryRules(int rulesetId)
    {
        try
        {
            using (await _itemCategoryRulesLock.WaitAsync($"{rulesetId}"))
            {
                using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
                PlanetmansDbContext dbContext = factory.GetDbContext();

                int storeRulesCount = await dbContext.RulesetItemCategoryRules
                    .Where(r => r.RulesetId == rulesetId)
                    .CountAsync();

                if (storeRulesCount > 0)
                {
                    return;
                }

                int defaultRulesetId = await dbContext.Rulesets
                    .Where(r => r.IsDefault).Select(r => r.Id)
                    .FirstOrDefaultAsync();

                List<RulesetItemCategoryRule> defaultRules = await dbContext.RulesetItemCategoryRules
                    .Where(r => r.RulesetId == defaultRulesetId)
                    .ToListAsync();

                dbContext.RulesetItemCategoryRules
                    .AddRange
                    (
                        defaultRules.Select
                        (
                            r => BuildRulesetItemCategoryRule
                            (
                                rulesetId,
                                r.ItemCategoryId,
                                r.Points,
                                r.IsBanned,
                                r.DeferToItemRules,
                                r.DeferToPlanetsideClassSettings,
                                new PlanetsideClassRuleSettings(r)
                            )
                        )
                    );

                await dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed new ruleset from default item category rules");
        }
    }

    private static RulesetItemCategoryRule BuildRulesetItemCategoryRule
    (
        int rulesetId,
        uint itemCategoryId,
        int points = 0,
        bool isBanned = false,
        bool deferToItemRules = false,
        bool deferToPlanetsideClassSettings = false,
        PlanetsideClassRuleSettings? planetsideClassSettings = null
    )
    {
        planetsideClassSettings ??= new PlanetsideClassRuleSettings();

        return new RulesetItemCategoryRule
        {
            RulesetId = rulesetId,
            ItemCategoryId = itemCategoryId,
            Points = points,
            IsBanned = isBanned,
            DeferToItemRules = deferToItemRules,

            DeferToPlanetsideClassSettings = deferToPlanetsideClassSettings,

            InfiltratorIsBanned = planetsideClassSettings.InfiltratorIsBanned,
            InfiltratorPoints = planetsideClassSettings.InfiltratorPoints,
            LightAssaultIsBanned = planetsideClassSettings.LightAssaultIsBanned,
            LightAssaultPoints = planetsideClassSettings.LightAssaultPoints,
            MedicIsBanned = planetsideClassSettings.MedicIsBanned,
            MedicPoints = planetsideClassSettings.MedicPoints,
            EngineerIsBanned = planetsideClassSettings.EngineerIsBanned,
            EngineerPoints = planetsideClassSettings.EngineerPoints,
            HeavyAssaultIsBanned = planetsideClassSettings.HeavyAssaultIsBanned,
            HeavyAssaultPoints = planetsideClassSettings.HeavyAssaultPoints,
            MaxIsBanned = planetsideClassSettings.MaxIsBanned,
            MaxPoints = planetsideClassSettings.MaxPoints
        };
    }

    private async Task SeedNewRulesetDefaultItemRules(int rulesetId)
    {
        try
        {
            using (await _itemRulesLock.WaitAsync($"{rulesetId}"))
            {
                using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
                PlanetmansDbContext dbContext = factory.GetDbContext();

                int storeRulesCount =
                    await dbContext.RulesetItemRules.Where(r => r.RulesetId == rulesetId)
                        .CountAsync(); // TODO: what is this check for?

                if (storeRulesCount > 0)
                {
                    return;
                }

                int defaultRulesetId = await dbContext.Rulesets.Where(r => r.IsDefault).Select(r => r.Id)
                    .FirstOrDefaultAsync();

                List<RulesetItemRule> defaultRules = await dbContext.RulesetItemRules
                    .Where(r => r.RulesetId == defaultRulesetId)
                    .ToListAsync();

                dbContext.RulesetItemRules
                    .AddRange
                    (
                        defaultRules.Select
                        (
                            r => BuildRulesetItemRule
                            (
                                rulesetId,
                                r.ItemId,
                                r.ItemCategoryId,
                                r.Points,
                                r.IsBanned,
                                r.DeferToPlanetsideClassSettings,
                                new PlanetsideClassRuleSettings(r)
                            )
                        )
                    );

                await dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed new ruleste from default item rules");
        }
    }

    private async Task UpdateItemRulesForItemCategoryRule(RulesetItemCategoryRule itemCategoryRule)
    {
        int rulesetId = itemCategoryRule.RulesetId;
        uint itemCategoryId = itemCategoryRule.ItemCategoryId;
        int itemCategoryPoints = itemCategoryRule.Points;
        bool isItemCategoryBanned = itemCategoryRule.IsBanned;

        using (await _itemRulesLock.WaitAsync($"{rulesetId}"))
        {
            try
            {
                using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
                PlanetmansDbContext dbContext = factory.GetDbContext();

                Dictionary<uint, RulesetItemRule> defaultItemRules
                    = GetRulesetItemRulesForItemCategoryId(DefaultRulesetId, itemCategoryId)
                        .ToDictionary(x => x.ItemId, x => x);

                Dictionary<uint, RulesetItemRule> storeItemRules
                    = GetRulesetItemRulesForItemCategoryId(rulesetId, itemCategoryId)
                        .ToDictionary(x => x.ItemId, x => x);

                IReadOnlyList<CensusItem>? allStoreItems = await _itemService.GetByCategoryAsync(itemCategoryId);
                if (allStoreItems is null)
                    return;

                List<RulesetItemRule> createdRules = new();

                foreach (CensusItem item in allStoreItems)
                {
                    defaultItemRules.TryGetValue(item.ItemId, out RulesetItemRule? defaultRule);
                    storeItemRules.TryGetValue(item.ItemId, out RulesetItemRule? storeRule);

                    if (storeRule == null)
                    {
                        if (defaultRule != null)
                        {
                            int points = itemCategoryPoints != 0 && defaultRule.Points != 0 ? itemCategoryPoints : defaultRule.Points;
                            bool isBanned = isItemCategoryBanned ? isItemCategoryBanned : defaultRule.IsBanned;

                            RulesetItemRule newRule = BuildRulesetItemRule(rulesetId, item.ItemId, itemCategoryId, points, isBanned);
                            createdRules.Add(newRule);
                        }
                        else
                        {
                            RulesetItemRule newRule = BuildRulesetItemRule(rulesetId, item.ItemId, itemCategoryId, itemCategoryPoints, isItemCategoryBanned);
                            createdRules.Add(newRule);
                        }
                    }
                    else
                    {
                        // Do Nothing, for now
                        // Other Considerations: updating storeRule.IsBanned and/or storeRule.Points to match ItemCategoryRule
                    }
                }

                if (createdRules.Any())
                {
                    dbContext.RulesetItemRules.AddRange(createdRules);
                }

                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update an item category's item rules");
            }
        }
    }

    private static RulesetItemRule BuildRulesetItemRule
    (
        int rulesetId,
        uint itemId,
        uint itemCategoryId,
        int points = 0,
        bool isBanned = false,
        bool deferToPlanetsideClassSettings = false,
        PlanetsideClassRuleSettings? planetsideClassSettings = null
    )
    {
        planetsideClassSettings ??= new PlanetsideClassRuleSettings();

        return new RulesetItemRule
        {
            RulesetId = rulesetId,
            ItemId = itemId,
            ItemCategoryId = itemCategoryId,
            Points = points,
            IsBanned = isBanned,

            DeferToPlanetsideClassSettings = deferToPlanetsideClassSettings,

            InfiltratorIsBanned = planetsideClassSettings.InfiltratorIsBanned,
            InfiltratorPoints = planetsideClassSettings.InfiltratorPoints,
            LightAssaultIsBanned = planetsideClassSettings.LightAssaultIsBanned,
            LightAssaultPoints = planetsideClassSettings.LightAssaultPoints,
            MedicIsBanned = planetsideClassSettings.MedicIsBanned,
            MedicPoints = planetsideClassSettings.MedicPoints,
            EngineerIsBanned = planetsideClassSettings.EngineerIsBanned,
            EngineerPoints = planetsideClassSettings.EngineerPoints,
            HeavyAssaultIsBanned = planetsideClassSettings.HeavyAssaultIsBanned,
            HeavyAssaultPoints = planetsideClassSettings.HeavyAssaultPoints,
            MaxIsBanned = planetsideClassSettings.MaxIsBanned,
            MaxPoints = planetsideClassSettings.MaxPoints
        };
    }

    private async Task SeedNewRulesetDefaultFacilityRules(int rulesetId)
    {
        using (await _facilityRulesLock.WaitAsync($"{rulesetId}"))
        {
            try
            {
                using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
                PlanetmansDbContext dbContext = factory.GetDbContext();

                int storeRulesCount = await dbContext.RulesetFacilityRules.Where(r => r.RulesetId == rulesetId).CountAsync();

                if (storeRulesCount > 0)
                {
                    return;
                }

                int defaultRulesetId = await dbContext.Rulesets.Where(r => r.IsDefault).Select(r => r.Id).FirstOrDefaultAsync();

                List<RulesetFacilityRule> defaultRules = await dbContext.RulesetFacilityRules.Where(r => r.RulesetId == defaultRulesetId).ToListAsync();

                dbContext.RulesetFacilityRules.AddRange(defaultRules.Select(r => BuildRulesetFacilityRule(rulesetId, r.FacilityId)));

                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed new ruleset from default facility rules");
            }
        }
    }

    private static RulesetFacilityRule BuildRulesetFacilityRule(int rulesetId, uint facilityId)
        => new()
        {
            RulesetId = rulesetId,
            FacilityId = facilityId
        };

    #endregion SAVE / UPDATE methods

    public async Task<bool> DeleteRulesetAsync(int rulesetId, CancellationToken ct = default)
    {
        using (await _rulesetLock.WaitAsync($"{rulesetId}", ct))
        {
            try
            {
                Ruleset? storeRuleset = await GetRulesetFromIdAsync(rulesetId, ct, false, false);

                if (storeRuleset == null || storeRuleset.IsDefault || storeRuleset.IsCustomDefault)
                {
                    return false;
                }

                if (!await CanDeleteRuleset(rulesetId, ct))
                {
                    return false;
                }

                using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
                PlanetmansDbContext dbContext = factory.GetDbContext();

                dbContext.Rulesets.Remove(storeRuleset);

                await dbContext.SaveChangesAsync(ct);
                await SetUpRulesetsMapAsync(ct);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting ruleset {ID}", rulesetId);

                return false;
            }
        }
    }

    #region Helper Methods

    private async Task SetUpRulesetsMapAsync(CancellationToken cancellationToken)
    {
        try
        {
            using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
            PlanetmansDbContext dbContext = factory.GetDbContext();

            List<Ruleset> rulesets = await dbContext.Rulesets.ToListAsync(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            foreach (int rulesetId in _rulesetsMap.Keys)
            {
                if (rulesets.All(t => t.Id != rulesetId))
                    _rulesetsMap.TryRemove(rulesetId, out _);
            }

            foreach (Ruleset ruleset in rulesets)
            {
                _rulesetsMap.AddOrUpdate(ruleset.Id, ruleset, (_, _) => ruleset);
            }

            Ruleset? customDefaultRuleset = rulesets.FirstOrDefault(r => r.IsCustomDefault);
            CustomDefaultRulesetId = customDefaultRuleset?.Id ?? DefaultRulesetId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed setting up RulesetsMap");
        }
    }

    public async Task<bool> CanDeleteRuleset(int rulesetId, CancellationToken cancellationToken)
    {
        if (rulesetId == CustomDefaultRulesetId || rulesetId == ActiveRulesetId || rulesetId == DefaultRulesetId)
            return false;

        bool hasBeenUsed = await HasRulesetBeenUsedAsync(rulesetId, cancellationToken);
        return !hasBeenUsed;
    }

    public async Task<bool> HasRulesetBeenUsedAsync(int rulesetId, CancellationToken cancellationToken)
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        return await dbContext.ScrimMatches.AnyAsync(m => m.RulesetId == rulesetId, cancellationToken);
    }

    #endregion Helper Methods

    #region Ruleset Activation / Defaulting / Favoriting
    public void SetActiveRulesetId(int rulesetId)
    {
        ActiveRulesetId = rulesetId;
    }

    public async Task<Ruleset?> SetCustomDefaultRulesetAsync(int rulesetId, CancellationToken ct = default)
    {
        _defaultRulesetAutoEvent.WaitOne();

        try
        {
            using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
            PlanetmansDbContext dbContext = factory.GetDbContext();

            Ruleset? currentDefaultRuleset = await dbContext.Rulesets
                .FirstOrDefaultAsync(r => r.IsCustomDefault, cancellationToken: ct);

            Ruleset? newDefaultRuleset = await GetRulesetFromIdAsync(rulesetId, ct, false, false);

            if (newDefaultRuleset == null)
            {
                _defaultRulesetAutoEvent.Set();
                return null;
            }

            if (currentDefaultRuleset == null)
            {
                newDefaultRuleset.IsCustomDefault = true;
                dbContext.Rulesets.Update(newDefaultRuleset);
            }
            else if (currentDefaultRuleset.Id != rulesetId)
            {
                currentDefaultRuleset.IsCustomDefault = false;
                dbContext.Rulesets.Update(currentDefaultRuleset);

                newDefaultRuleset.IsCustomDefault = true;
                dbContext.Rulesets.Update(newDefaultRuleset);
            }
            else
            {
                _defaultRulesetAutoEvent.Set();

                CustomDefaultRulesetId = currentDefaultRuleset.Id;

                return currentDefaultRuleset;
            }

            await dbContext.SaveChangesAsync(ct);

            _defaultRulesetAutoEvent.Set();

            _logger.LogInformation("Set ruleset {ID} as new custom default ruleset", rulesetId);

            CustomDefaultRulesetId = newDefaultRuleset.Id;

            return newDefaultRuleset;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed setting ruleset {ID} as new custom default ruleset", rulesetId);

            _defaultRulesetAutoEvent.Set();

            return null;
        }
    }
    #endregion Ruleset Activation / Defaulting / Favoriting

    #region Import / Export JSON
    public async Task<bool> ExportRulesetToJsonFile(int rulesetId, CancellationToken ct)
    {
        using (await _rulesetExportLock.WaitAsync($"{rulesetId}", ct))
        {
            try
            {
                Ruleset? ruleset = await GetRulesetFromIdAsync(rulesetId, ct);
                if (ruleset is null)
                    return false;

                string fileName = GetRulesetFileName(rulesetId, ruleset.Name);

                if (await _rulesetFileService.WriteToJsonFileAsync(fileName, new JsonRuleset(ruleset, fileName), ct))
                {
                    _logger.LogInformation("Exported ruleset {ID} to file {FileName}", rulesetId, fileName);
                    return true;
                }

                _logger.LogError("Failed exporting ruleset {ID} to JSON file", rulesetId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed exporting ruleset {ID} to JSON file", rulesetId);
                return false;
            }
        }
    }

    public async Task<Ruleset?> ImportNewRulesetFromJsonFile
    (
        string fileName,
        bool returnCollections = false,
        bool returnOverlayConfiguration = false,
        CancellationToken ct = default
    )
    {
        try
        {
            JsonRuleset? jsonRuleset = await _rulesetFileService.ReadFromJsonFileAsync(fileName, ct);
            if (jsonRuleset is null)
                return null;

            Ruleset? ruleset = await CreateRulesetAsync(ConvertToDbModel(jsonRuleset, fileName), ct);
            if (ruleset is null)
            {
                _logger.LogWarning
                (
                    "Failed importing ruleset from file {Name}: failed creating new ruleset entity",
                    fileName
                );

                return null;
            }

            List<Task> TaskList = new();

            if (jsonRuleset.RulesetOverlayConfiguration != null)
            {
                ruleset.RulesetOverlayConfiguration = ConvertToDbModel(ruleset.Id, jsonRuleset.RulesetOverlayConfiguration);
                Task<bool> overlayConfigurationTask = SaveRulesetOverlayConfiguration(ruleset.Id, ruleset.RulesetOverlayConfiguration, ct);
                TaskList.Add(overlayConfigurationTask);
            }
            else
            {
                RulesetOverlayConfiguration defaultOverlayConfiguration = BuildRulesetOverlayConfiguration(ruleset.Id);
                ruleset.RulesetOverlayConfiguration = defaultOverlayConfiguration;
                Task<bool> overlayConfigurationTask = SaveRulesetOverlayConfiguration(ruleset.Id, ruleset.RulesetOverlayConfiguration, ct);
                TaskList.Add(overlayConfigurationTask);
            }

            if (jsonRuleset.RulesetActionRules != null && jsonRuleset.RulesetActionRules.Any())
            {
                ruleset.RulesetActionRules = jsonRuleset.RulesetActionRules.Select(r => ConvertToDbModel(ruleset.Id, r)).ToList();
                Task actionRulesTask = SaveRulesetActionRules(ruleset.Id, ruleset.RulesetActionRules, ct);
                TaskList.Add(actionRulesTask);
            }

            if (jsonRuleset.RulesetItemCategoryRules != null && jsonRuleset.RulesetItemCategoryRules.Any())
            {
                ruleset.RulesetItemCategoryRules = jsonRuleset.RulesetItemCategoryRules
                    .Select(r => ConvertToDbModel(ruleset.Id, r))
                    .ToList();

                Task itemCategoryRulesTask = SaveRulesetItemCategoryRules(ruleset.Id, ruleset.RulesetItemCategoryRules, ct);
                TaskList.Add(itemCategoryRulesTask);

                List<RulesetItemRule> rulesetItemRules = new();

                foreach (JsonRulesetItemCategoryRule jsonItemCategoryRule in jsonRuleset.RulesetItemCategoryRules)
                {
                    if (jsonItemCategoryRule.RulesetItemRules is null
                        || jsonItemCategoryRule.RulesetItemRules.Count is 0)
                        continue;

                    foreach (JsonRulesetItemRule jsonItemRule in jsonItemCategoryRule.RulesetItemRules)
                    {
                        if (rulesetItemRules.All(r => r.ItemId != jsonItemRule.ItemId))
                        {
                            rulesetItemRules.Add
                            (
                                ConvertToDbModel(ruleset.Id, jsonItemRule, jsonItemCategoryRule.ItemCategoryId)
                            );
                        }
                    }
                }

                ruleset.RulesetItemRules = new List<RulesetItemRule>(rulesetItemRules);

                Task itemRulesTask = SaveRulesetItemRules(ruleset.Id, ruleset.RulesetItemRules, ct);
                TaskList.Add(itemRulesTask);
            }

            if (jsonRuleset.RulesetFacilityRules != null && jsonRuleset.RulesetFacilityRules.Any())
            {
                ruleset.RulesetFacilityRules = jsonRuleset.RulesetFacilityRules.Select(r => ConvertToDbModel(ruleset.Id, r)).ToList();
                Task facilityRulesTask = SaveRulesetFacilityRules(ruleset.Id, ruleset.RulesetFacilityRules);
                TaskList.Add(facilityRulesTask);
            }

            if (TaskList.Any())
                await Task.WhenAll(TaskList);

            _logger.LogInformation("Created ruleset {ID} from file {File}", ruleset.Id, fileName);

            if (!returnCollections)
            {
                ruleset.RulesetActionRules?.Clear();
                ruleset.RulesetItemCategoryRules?.Clear();
                ruleset.RulesetItemRules?.Clear();
                ruleset.RulesetFacilityRules?.Clear();
            }

            if (!returnOverlayConfiguration)
                ruleset.RulesetOverlayConfiguration = null;

            _logger.LogInformation("Imported ruleset {FileName}", fileName);
            return ruleset;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import new ruleset from file {FileName}", fileName);
            return null;
        }
    }

    // Save Rules From JSON Import
    private async Task SaveRulesetFacilityRules(int rulesetId, IEnumerable<RulesetFacilityRule> rules)
    {
        if (rulesetId == DefaultRulesetId)
        {
            return;
        }

        using (await _facilityRulesLock.WaitAsync($"{rulesetId}"))
        {
            List<RulesetFacilityRule> ruleUpdates = rules.Where(rule => rule.RulesetId == rulesetId).ToList();
            if (ruleUpdates.Count is 0)
                return;

            try
            {
                using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
                PlanetmansDbContext dbContext = factory.GetDbContext();

                List<RulesetFacilityRule> storeRules = await dbContext.RulesetFacilityRules.Where(rule => rule.RulesetId == rulesetId).ToListAsync();
                List<RulesetFacilityRule> allRules = new(storeRules);
                allRules.AddRange(ruleUpdates);

                List<RulesetFacilityRule> newEntities = new();

                foreach (RulesetFacilityRule rule in allRules)
                {
                    RulesetFacilityRule? storeEntity = storeRules.FirstOrDefault(r => r.FacilityId == rule.FacilityId);
                    RulesetFacilityRule? updateRule = ruleUpdates.FirstOrDefault(r => r.FacilityId == rule.FacilityId);

                    if (storeEntity == null)
                    {
                        newEntities.Add(rule);
                    }
                    else if (updateRule == null)
                    {
                        dbContext.RulesetFacilityRules.Remove(storeEntity);
                    }
                    else
                    {
                        storeEntity = updateRule;
                        dbContext.RulesetFacilityRules.Update(storeEntity);
                    }
                }

                if (newEntities.Any())
                {
                    dbContext.RulesetFacilityRules.AddRange(newEntities);
                }

                Ruleset? storeRuleset = await dbContext.Rulesets.Where(r => r.Id == rulesetId).FirstOrDefaultAsync();
                if (storeRuleset != null)
                {
                    storeRuleset.DateLastModified = DateTime.UtcNow;
                    dbContext.Rulesets.Update(storeRuleset);

                    RulesetRuleChangeMessage message = new(storeRuleset, RulesetRuleChangeType.ItemCategoryRule);
                    _logger.LogInformation("{RulesetRuleChangeInfo}", message.Info);
                    _messageService.BroadcastRulesetRuleChangeMessage(message);
                }

                await dbContext.SaveChangesAsync();
                //_logger.LogInformation($"Saved Facility Rule updates for Ruleset {rulesetId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving SaveRulesetFacilityRules to database");
            }
        }
    }

    public IEnumerable<string> GetJsonRulesetFileNames()
        => _rulesetFileService.GetJsonRulesetFileNames();

    private static string GetRulesetFileName(int rulesetId, string rulesetName)
    {
        char[] characters = $"rs{rulesetId}_{rulesetName}".ToCharArray();

        characters = Array.FindAll
        (
            characters,
            c => char.IsLetterOrDigit(c)
                || char.IsWhiteSpace(c)
                || c is '-' or '_'
        );

        return new string(characters);
    }

    private static Ruleset ConvertToDbModel(JsonRuleset jsonRuleset, string sourceFileName)
    {
        return new Ruleset
        {
            Name = jsonRuleset.Name,
            DateCreated = jsonRuleset.DateCreated,
            DateLastModified = jsonRuleset.DateLastModified,
            IsDefault = jsonRuleset.IsDefault,
            IsCustomDefault = false,
            DefaultMatchTitle = jsonRuleset.DefaultMatchTitle,
            DefaultRoundLength = jsonRuleset.DefaultRoundLength,
            DefaultEndRoundOnFacilityCapture = jsonRuleset.DefaultEndRoundOnFacilityCapture,
            SourceFile = sourceFileName
        };
    }

    private RulesetOverlayConfiguration ConvertToDbModel(int rulesetId, JsonRulesetOverlayConfiguration jsonConfiguration)
    {
        bool useCompactLayout = jsonConfiguration.UseCompactLayout ?? false;
        OverlayStatsDisplayType statsDisplayType = jsonConfiguration.StatsDisplayType ?? OverlayStatsDisplayType.InfantryScores;

        return BuildRulesetOverlayConfiguration(rulesetId, useCompactLayout, statsDisplayType, jsonConfiguration.UseCompactLayout);
    }

    private static RulesetActionRule ConvertToDbModel(int rulesetId, JsonRulesetActionRule jsonRule)
        => BuildRulesetActionRule
        (
            rulesetId,
            jsonRule.ScrimActionType,
            jsonRule.Points,
            jsonRule.DeferToItemCategoryRules
        );

    private static RulesetItemCategoryRule ConvertToDbModel(int rulesetId, JsonRulesetItemCategoryRule jsonRule)
        => BuildRulesetItemCategoryRule
        (
            rulesetId,
            jsonRule.ItemCategoryId,
            jsonRule.Points,
            jsonRule.IsBanned,
            jsonRule.DeferToItemRules,
            jsonRule.DeferToPlanetsideClassSettings,
            new PlanetsideClassRuleSettings(jsonRule)
        );

    private static RulesetItemRule ConvertToDbModel(int rulesetId, JsonRulesetItemRule jsonRule, uint itemCategoryId)
        => BuildRulesetItemRule
        (
            rulesetId,
            jsonRule.ItemId,
            itemCategoryId,
            jsonRule.Points,
            jsonRule.IsBanned,
            jsonRule.DeferToPlanetsideClassSettings,
            new PlanetsideClassRuleSettings(jsonRule)
        );

    private RulesetFacilityRule ConvertToDbModel(int rulesetID, JsonRulesetFacilityRule jsonRule)
        => BuildRulesetFacilityRule(rulesetID, jsonRule.FacilityId);

    #endregion Import / Export JSON

    public static bool IsValidRulesetName(string name)
        => _rulesetNameRegex.Match(name).Success;

    public static bool IsValidRulesetDefaultRoundLength(int seconds)
        => seconds > 0;

    public static bool IsValidRulesetDefaultMatchTitle(string title)
        => _rulesetDefaultMatchTitleRegex.Match(title).Success || string.IsNullOrWhiteSpace(title);

    [GeneratedRegex("^([A-Za-z0-9()\\[\\]\\-_'.][ ]{0,1}){1,49}[A-Za-z0-9()\\[\\]\\-_'.]$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex GetRulesetNameRegex();
}
