using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using squittal.ScrimPlanetmans.App.Models.CensusRest;
using squittal.ScrimPlanetmans.App.ScrimMatch.Events;
using squittal.ScrimPlanetmans.App.ScrimMatch.Interfaces;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;
using squittal.ScrimPlanetmans.App.Services.ScrimMatch.Interfaces;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset;

public class ScrimRulesetManager : IScrimRulesetManager
{
    private const int DEFAULT_RULESET_ID = 1;

    private readonly ILogger<ScrimRulesetManager> _logger;
    private readonly IDbContextHelper _dbContextHelper;
    private readonly IItemCategoryService _itemCategoryService;
    private readonly ICensusItemService _itemService;
    private readonly IRulesetDataService _rulesetDataService;
    private readonly IScrimMessageBroadcastService _messageService;

    public Models.Ruleset? ActiveRuleset { get; private set; }
    private readonly AutoResetEvent _activateRulesetAutoEvent = new(true);

    public ScrimRulesetManager
    (
        IDbContextHelper dbContextHelper,
        IItemCategoryService itemCategoryService,
        ICensusItemService itemService,
        IRulesetDataService rulesetDataService,
        IScrimMessageBroadcastService messageService,
        ILogger<ScrimRulesetManager> logger
    )
    {
        _dbContextHelper = dbContextHelper;
        _itemCategoryService = itemCategoryService;
        _itemService = itemService;
        _rulesetDataService = rulesetDataService;
        _messageService = messageService;
        _logger = logger;

        _messageService.RaiseRulesetRuleChangeEvent += HandleRulesetRuleChangeMesssage;
        _messageService.RaiseRulesetSettingChangeEvent += HandleRulesetSettingChangeMessage;
        _messageService.RaiseRulesetOverlayConfigurationChangeEvent += HandleRulesetOverlayConfigurationChangeMessage;
    }

    public async Task<IEnumerable<Models.Ruleset>> GetRulesetsAsync(CancellationToken ct)
        =>  await _rulesetDataService.GetAllRulesetsAsync(ct);

    public async Task<Models.Ruleset?> GetActiveRulesetAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        if (ActiveRuleset == null)
        {
            await ActivateDefaultRulesetAsync(ct);
            return ActiveRuleset;
        }

        if (forceRefresh
            || ActiveRuleset.RulesetActionRules == null
            || !ActiveRuleset.RulesetActionRules.Any()
            || ActiveRuleset.RulesetItemCategoryRules == null
            || !ActiveRuleset.RulesetItemCategoryRules.Any())
        {
            await SetUpActiveRulesetAsync(ct);
        }

        return ActiveRuleset;
    }

    public async Task<bool> ActivateRulesetAsync(int rulesetId, CancellationToken ct = default)
    {
        _activateRulesetAutoEvent.WaitOne();

        try
        {
            if (ActiveRuleset?.Id == rulesetId)
                return true;

            Models.Ruleset? currentActiveRuleset = ActiveRuleset;
 
            Models.Ruleset? newActiveRuleset = await _rulesetDataService.GetRulesetFromIdAsync(rulesetId, ct);
            if (newActiveRuleset == null)
                return false;

            _rulesetDataService.SetActiveRulesetId(rulesetId);
            ActiveRuleset = newActiveRuleset;

            ActiveRulesetChangeMessage message = currentActiveRuleset == null
                ? new ActiveRulesetChangeMessage(ActiveRuleset)
                : new ActiveRulesetChangeMessage(ActiveRuleset, currentActiveRuleset);

            _messageService.BroadcastActiveRulesetChangeMessage(message);
            _logger.LogInformation("Active ruleset loaded: {Name}", ActiveRuleset.Name);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate a ruleset");
            return false;
        }
        finally
        {
            _activateRulesetAutoEvent.Set();
        }
    }

    public async Task<bool> ActivateDefaultRulesetAsync(CancellationToken ct = default)
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        Models.Ruleset? ruleset = await dbContext.Rulesets.FirstOrDefaultAsync
        (
            r => r.IsCustomDefault,
            cancellationToken: ct
        );

        if (ruleset is null)
        {
            _logger.LogDebug("No custom default ruleset found. Loading default ruleset...");
            ruleset = await dbContext.Rulesets.FirstOrDefaultAsync(r => r.IsDefault, cancellationToken: ct);
        }

        if (ruleset is null)
        {
            _logger.LogError("Failed to activate default ruleset: no ruleset found");
            return false;
        }

        await ActivateRulesetAsync(ruleset.Id, ct);
        return true;
    }

    public async Task SetUpActiveRulesetAsync(CancellationToken ct = default)
    {
        _activateRulesetAutoEvent.WaitOne();

        try
        {
            Models.Ruleset? currentActiveRuleset = ActiveRuleset;

            if (currentActiveRuleset == null)
            {
                _logger.LogError($"Failed to set up active ruleset: no ruleset found");

                _activateRulesetAutoEvent.Set();

                return;
            }

            Models.Ruleset? tempRuleset = await _rulesetDataService.GetRulesetFromIdAsync(currentActiveRuleset.Id, ct);

            if (tempRuleset == null)
            {
                _logger.LogError($"Failed to set up active ruleset: temp ruleset is null");

                _activateRulesetAutoEvent.Set();

                return;
            }

            ActiveRuleset = tempRuleset;

            _rulesetDataService.SetActiveRulesetId(ActiveRuleset.Id);

            _logger.LogInformation("Active ruleset collections loaded: {Name}", ActiveRuleset.Name);

            _activateRulesetAutoEvent.Set();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set up active ruleset");

            _activateRulesetAutoEvent.Set();
        }
    }

    private void HandleRulesetRuleChangeMesssage(object? sender, ScrimMessageEventArgs<RulesetRuleChangeMessage> e)
    {
        int changedRulesetId = e.Message.Ruleset.Id;

        if (changedRulesetId != ActiveRuleset?.Id)
            return;

        try
        {
            SetUpActiveRulesetAsync().Wait();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to setup active ruleset after recieving change message");
        }
    }

    private void HandleRulesetSettingChangeMessage(object? sender, ScrimMessageEventArgs<RulesetSettingChangeMessage> e)
    {
        if (ActiveRuleset is null)
            return;

        Models.Ruleset ruleset = e.Message.Ruleset;

        _activateRulesetAutoEvent.WaitOne();

        if (ruleset.Id != ActiveRuleset.Id)
        {
            _activateRulesetAutoEvent.Set();
            return;
        }

        ActiveRuleset.Name = ruleset.Name;
        ActiveRuleset.DefaultMatchTitle = ruleset.DefaultMatchTitle;
        ActiveRuleset.DefaultRoundLength = ruleset.DefaultRoundLength;
        ActiveRuleset.DefaultEndRoundOnFacilityCapture = ruleset.DefaultEndRoundOnFacilityCapture;

        _activateRulesetAutoEvent.Set();
    }

    private void HandleRulesetOverlayConfigurationChangeMessage(object? sender, ScrimMessageEventArgs<RulesetOverlayConfigurationChangeMessage> e)
    {
        if (ActiveRuleset is null)
            return;

        Models.Ruleset ruleset = e.Message.Ruleset;
        RulesetOverlayConfiguration overlayConfiguration = e.Message.OverlayConfiguration;

        _activateRulesetAutoEvent.WaitOne();

        if (ruleset.Id != ActiveRuleset.Id)
        {
            _activateRulesetAutoEvent.Set();
            return;
        }

        if (ActiveRuleset.RulesetOverlayConfiguration is not null)
        {
            ActiveRuleset.RulesetOverlayConfiguration.UseCompactLayout = overlayConfiguration.UseCompactLayout;
            ActiveRuleset.RulesetOverlayConfiguration.StatsDisplayType = overlayConfiguration.StatsDisplayType;
            ActiveRuleset.RulesetOverlayConfiguration.ShowStatusPanelScores = overlayConfiguration.ShowStatusPanelScores;
        }

        _activateRulesetAutoEvent.Set();
    }

    public async Task<Models.Ruleset?> GetDefaultRulesetAsync(CancellationToken ct = default)
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        Models.Ruleset? ruleset = await dbContext.Rulesets.FirstOrDefaultAsync(r => r.IsDefault, cancellationToken: ct);

        if (ruleset == null)
        {
            return null;
        }

        ruleset = await _rulesetDataService.GetRulesetFromIdAsync(ruleset.Id, ct);

        return ruleset;
    }

    public async Task SeedDefaultRulesetAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Seeding Default Ruleset...");

        Stopwatch stopWatchSetup = new();
        Stopwatch stopWatchCollections = new();
        Stopwatch stopWatchOverlay = new();
        Stopwatch stopWatchActionRules = new();
        Stopwatch stopWatchItemRules = new();
        Stopwatch stopWatchItemCategoryRules = new();
        Stopwatch stopWatchFacilityRules = new();
        Stopwatch stopWatchFinalize = new();

        Stopwatch stopWatchTotal = Stopwatch.StartNew();
        stopWatchSetup.Start();

        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        Models.Ruleset? storeRuleset = await dbContext.Rulesets.FirstOrDefaultAsync
        (
            r => r.Id == DEFAULT_RULESET_ID,
            cancellationToken: ct
        );

        bool rulesetExistsInDb = false;

        RulesetOverlayConfiguration? storeOverlayConfiguration = null;

        List<RulesetActionRule>? storeActionRules = new();
        List<RulesetItemCategoryRule>? storeItemCategoryRules = new();
        List<RulesetItemRule>? storeItemRules = null;
        List<RulesetFacilityRule>? storeFacilityRules = new();

        if (storeRuleset != null)
        {
            stopWatchCollections.Start();

            Models.Ruleset? storeRulesetWithCollections = await _rulesetDataService.GetRulesetFromIdAsync
            (
                storeRuleset.Id,
                ct
            );

            if (storeRulesetWithCollections is not null)
            {
                storeOverlayConfiguration = storeRulesetWithCollections.RulesetOverlayConfiguration;

                if (storeRulesetWithCollections.RulesetActionRules is not null)
                    storeActionRules = storeRulesetWithCollections.RulesetActionRules.ToList();
                if (storeRulesetWithCollections.RulesetItemCategoryRules is not null)
                    storeItemCategoryRules = storeRulesetWithCollections.RulesetItemCategoryRules.ToList();
                if (storeRulesetWithCollections.RulesetItemRules is not null)
                    storeItemRules = storeRulesetWithCollections.RulesetItemRules.ToList();
                if (storeRulesetWithCollections.RulesetFacilityRules is not null)
                    storeFacilityRules = storeRulesetWithCollections.RulesetFacilityRules.ToList();
            }

            rulesetExistsInDb = true;
            stopWatchCollections.Stop();
        }
        else
        {
            DateTime utcNow = DateTime.UtcNow;
            Models.Ruleset newRuleset = new()
            {
                Name = "Default",
                DateCreated = utcNow
            };

            storeRuleset = newRuleset;
        }

        storeRuleset.DefaultMatchTitle = "PS2 Scrims";
        storeRuleset.IsDefault = true;
        storeRuleset.DefaultEndRoundOnFacilityCapture = false;


        stopWatchSetup.Stop();

        stopWatchOverlay.Start();

        #region Overlay Configuration

        if (storeOverlayConfiguration == null)
        {
            storeOverlayConfiguration = new RulesetOverlayConfiguration()
            {
                RulesetId = DEFAULT_RULESET_ID,
                UseCompactLayout = false,
                StatsDisplayType = OverlayStatsDisplayType.InfantryScores,
                ShowStatusPanelScores = null
            };

            dbContext.RulesetOverlayConfigurations.Add(storeOverlayConfiguration);
        }
        else
        {
            storeOverlayConfiguration.UseCompactLayout = false;
            storeOverlayConfiguration.StatsDisplayType = OverlayStatsDisplayType.InfantryScores;
            storeOverlayConfiguration.ShowStatusPanelScores = null;

            dbContext.RulesetOverlayConfigurations.Update(storeOverlayConfiguration);
        }

        #endregion Overlay Configuration

        stopWatchOverlay.Stop();

        stopWatchActionRules.Start();

        #region Action rules

        RulesetActionRule[] defaultActionRules = GetDefaultActionRules();
        List<RulesetActionRule> createdActionRules = new();
        List<RulesetActionRule> allActionRules = new();

        List<ScrimActionType> allActionEnumValues = Enum.GetValues<ScrimActionType>()
            .Where
            (
                a => a is not (ScrimActionType.None or ScrimActionType.Login or ScrimActionType.Logout)
            )
            .ToList();

        List<ScrimActionType> allActionValues = new();
        allActionValues.AddRange(allActionEnumValues);
        allActionValues.AddRange(storeActionRules.Select(ar => ar.ScrimActionType).Where(a => !allActionValues.Contains(a)).ToList());

        foreach (ScrimActionType actionType in allActionValues)
        {
            RulesetActionRule? storeEntity = storeActionRules?.FirstOrDefault(r => r.ScrimActionType == actionType);
            RulesetActionRule? defaultEntity = defaultActionRules.FirstOrDefault(r => r.ScrimActionType == actionType);

            bool isValidAction = storeEntity == null || allActionEnumValues.Any(enumValue => enumValue == storeEntity.ScrimActionType);

            if (storeEntity == null)
            {
                if (defaultEntity != null)
                {
                    defaultEntity.RulesetId = DEFAULT_RULESET_ID;
                    createdActionRules.Add(defaultEntity);
                    allActionRules.Add(defaultEntity);
                }
                else
                {
                    RulesetActionRule newEntity = BuildRulesetActionRule(DEFAULT_RULESET_ID, actionType);
                    createdActionRules.Add(newEntity);
                    allActionRules.Add(newEntity);
                }
            }
            else if (isValidAction)
            {
                if (defaultEntity != null)
                {
                    storeEntity.Points = defaultEntity.Points;
                    storeEntity.DeferToItemCategoryRules = defaultEntity.DeferToItemCategoryRules;
                    storeEntity.ScrimActionTypeDomain = defaultEntity.ScrimActionTypeDomain;
                }
                else
                {
                    storeEntity.Points = 0;
                    storeEntity.ScrimActionTypeDomain = ScrimAction.GetDomainFromActionType(storeEntity.ScrimActionType);
                }

                dbContext.RulesetActionRules.Update(storeEntity);
                allActionRules.Add(storeEntity);
            }
            else
            {
                dbContext.RulesetActionRules.Remove(storeEntity);
            }
        }

        if (createdActionRules.Any())
        {
            dbContext.RulesetActionRules.AddRange(createdActionRules);
        }
        #endregion Action rules

        stopWatchActionRules.Stop();

        stopWatchItemCategoryRules.Start();

        #region Item Category Rules

        IEnumerable<uint>? allWeaponItemCategoryIds = await _itemCategoryService.GetWeaponItemCategoryIdsAsync(ct);

        RulesetItemCategoryRule[] defaultItemCategoryRules = GetDefaultItemCategoryRules();
        List<RulesetItemCategoryRule> createdItemCategoryRules = new();

        List<RulesetItemCategoryRule> allItemCategoryRules = new();

        foreach (uint categoryId in allWeaponItemCategoryIds)
        {
            RulesetItemCategoryRule? storeEntity = storeItemCategoryRules?.FirstOrDefault(r => r.ItemCategoryId == categoryId);
            RulesetItemCategoryRule? defaultEntity = defaultItemCategoryRules.FirstOrDefault(r => r.ItemCategoryId == categoryId);

            if (storeEntity is null)
            {
                if (defaultEntity is not null)
                {
                    defaultEntity.RulesetId = DEFAULT_RULESET_ID;
                    createdItemCategoryRules.Add(defaultEntity);
                    allItemCategoryRules.Add(defaultEntity);
                }
                else
                {
                    RulesetItemCategoryRule newEntity = BuildRulesetItemCategoryRule(DEFAULT_RULESET_ID, categoryId);
                    createdItemCategoryRules.Add(newEntity);
                    allItemCategoryRules.Add(newEntity);
                }
            }
            else
            {
                storeEntity.Points = defaultEntity?.Points ?? 0;
                storeEntity.IsBanned = defaultEntity?.IsBanned ?? false;
                storeEntity.DeferToItemRules = defaultEntity?.DeferToItemRules ?? false;

                dbContext.RulesetItemCategoryRules.Update(storeEntity);
                allItemCategoryRules.Add(storeEntity);
            }
        }

        if (createdItemCategoryRules.Any())
        {
            dbContext.RulesetItemCategoryRules.AddRange(createdItemCategoryRules);
        }
        #endregion Item Category Rules

        stopWatchItemCategoryRules.Stop();

        stopWatchItemRules.Start();

        #region Item Rules

        IReadOnlyList<CensusItem>? allWeaponItems = await _itemService.GetAllWeaponsAsync(ct);

        RulesetItemRule[] defaultItemRules = GetDefaultItemRules();
        List<RulesetItemRule> createdItemRules = new();

        List<uint> allItemIds = new(defaultItemRules.Select(r => r.ItemId));
        if (storeItemRules != null)
        {
            allItemIds.AddRange(storeItemRules.Where(r => !allItemIds.Contains(r.ItemId)).Select(r => r.ItemId));
        }

        allItemIds.AddRange(allWeaponItems.Where(r => !allItemIds.Contains(r.ItemId)).Select(r => r.ItemId));

        List<RulesetItemRule> allItemRules = new();

        foreach (uint itemId in allItemIds)
        {
            bool isWeaponItem = allWeaponItems.Any(r => r.ItemId == itemId);
            RulesetItemRule? storeEntity = storeItemRules?.FirstOrDefault(r => r.ItemId == itemId);
            RulesetItemRule? defaultEntity = defaultItemRules.FirstOrDefault(r => r.ItemId == itemId);
            uint? categoryId = allWeaponItems.FirstOrDefault(i => i.ItemId == itemId)?.ItemCategoryId;

            bool categoryDefersToItems = false;

            if (categoryId != null)
            {
                categoryDefersToItems = allItemCategoryRules.Any(r => r.ItemCategoryId == categoryId && r.DeferToItemRules);
            }

            if (storeEntity == null)
            {
                if (defaultEntity != null)
                {
                    defaultEntity.RulesetId = DEFAULT_RULESET_ID;

                    createdItemRules.Add(defaultEntity);
                    allItemRules.Add(defaultEntity);
                }
                else if (isWeaponItem && categoryDefersToItems)
                {
                    int defaultPoints = allItemCategoryRules.Where(r => r.ItemCategoryId == categoryId)
                        .Select(r => r.Points)
                        .FirstOrDefault();

                    RulesetItemRule newEntity = BuildRulesetItemRule(DEFAULT_RULESET_ID, itemId, categoryId ?? 0, defaultPoints);

                    createdItemRules.Add(newEntity);
                    allItemRules.Add(newEntity);
                }
            }
            else
            {
                if (defaultEntity != null)
                {
                    if (categoryId.HasValue && categoryId != defaultEntity.ItemCategoryId)
                    {
                        defaultEntity.ItemCategoryId = categoryId.Value;
                    }

                    defaultEntity.RulesetId = DEFAULT_RULESET_ID;

                    storeEntity = defaultEntity;

                    dbContext.RulesetItemRules.Update(storeEntity);
                    allItemRules.Add(storeEntity);
                }
                else if (!isWeaponItem || !categoryDefersToItems)
                {
                    dbContext.RulesetItemRules.Remove(storeEntity);
                }
            }
        }

        if (createdItemRules.Any())
        {
            dbContext.RulesetItemRules.AddRange(createdItemRules);
        }
        #endregion Item Rules

        stopWatchItemRules.Stop();

        stopWatchFacilityRules.Start();

        #region Facility Rules
        RulesetFacilityRule[] defaultFacilityRules = GetDefaultFacilityRules();

        List<RulesetFacilityRule> createdFacilityRules = new();

        List<RulesetFacilityRule> allFacilityRules = new(storeFacilityRules);
        allFacilityRules.AddRange(defaultFacilityRules.Where(d => allFacilityRules.All(a => a.FacilityId != d.FacilityId)));

        foreach (RulesetFacilityRule facilityRule in allFacilityRules)
        {
            RulesetFacilityRule? storeEntity = storeFacilityRules?.FirstOrDefault(r => r.FacilityId == facilityRule.FacilityId);
            RulesetFacilityRule? defaultEntity = defaultFacilityRules.FirstOrDefault(r => r.FacilityId == facilityRule.FacilityId);

            if (storeEntity == null)
            {
                if (defaultEntity != null)
                {
                    defaultEntity.RulesetId = DEFAULT_RULESET_ID;

                    createdFacilityRules.Add(defaultEntity);

                }
            }
            else
            {
                if (defaultEntity == null)
                {
                    dbContext.RulesetFacilityRules.Remove(storeEntity);
                    allFacilityRules.Remove(storeEntity);
                }
            }
        }

        if (createdFacilityRules.Any())
        {
            dbContext.RulesetFacilityRules.AddRange(createdFacilityRules);
        }
        #endregion Facility Rules

        stopWatchFacilityRules.Stop();

        stopWatchFinalize.Start();

        storeRuleset.RulesetOverlayConfiguration = storeOverlayConfiguration;

        storeRuleset.RulesetActionRules = allActionRules;
        storeRuleset.RulesetItemCategoryRules = allItemCategoryRules;
        storeRuleset.RulesetItemRules = allItemRules;
        storeRuleset.RulesetFacilityRules = allFacilityRules;

        if (rulesetExistsInDb)
        {
            dbContext.Rulesets.Update(storeRuleset);
        }
        else
        {
            dbContext.Rulesets.Add(storeRuleset);
        }

        await dbContext.SaveChangesAsync(ct);

        stopWatchFinalize.Stop();
        stopWatchTotal.Stop();

        _logger.LogInformation
        (
            "Finished seeding default ruleset (elapsed: {TotalTime})" +
            "\n\tSetup: {SetupTime}" +
            "\n\t\t Get Collections: {CollectionsTime}" +
            "\n\t\tOverlay Configuration: {OverlayTime}" +
            "\n\tAction Rules: {ActionsTime}" +
            "\n\tItem Category Rules: {ItemCategoriesTime}" +
            "\n\tItem Rules: {ItemsTime}" +
            "\n\tFacility Rules: {FacilitiesTime}" +
            "\n\tFinalize: {FinalizeTime}",
            stopWatchTotal.Elapsed,
            stopWatchSetup.Elapsed,
            stopWatchCollections.Elapsed,
            stopWatchOverlay.Elapsed,
            stopWatchActionRules.Elapsed,
            stopWatchItemCategoryRules.Elapsed,
            stopWatchItemRules.Elapsed,
            stopWatchFacilityRules.Elapsed,
            stopWatchFinalize.Elapsed
        );
    }

    private static RulesetItemCategoryRule[] GetDefaultItemCategoryRules()
    {
        return new[]
        {
            BuildRulesetItemCategoryRule(2, 1, false, true),    // Knife
            BuildRulesetItemCategoryRule(3, 1, false, true),    // Pistol
            BuildRulesetItemCategoryRule(4, 1, false, true),    // Shotgun
            BuildRulesetItemCategoryRule(5, 1, false, true),    // SMG
            BuildRulesetItemCategoryRule(6, 1, false, true),    // LMG
            BuildRulesetItemCategoryRule(7, 1, false, true),    // Assault Rifle
            BuildRulesetItemCategoryRule(8, 1, false, true),    // Carbine
            BuildRulesetItemCategoryRule(11, 1, false, true),   // Sniper Rifle
            BuildRulesetItemCategoryRule(13, 0, false, true),   // Rocket Launcher
            BuildRulesetItemCategoryRule(24, 1, false, false),  // Crossbow
            BuildRulesetItemCategoryRule(100, 1, false, false), // Infantry (Nothing)
            BuildRulesetItemCategoryRule(157, 1, false, true),  // Hybrid Rifle (NSX-A Kuwa)

            // Universal Bans
            BuildRulesetItemCategoryRule(12, 0, true, false),  // Scout Rifle
            BuildRulesetItemCategoryRule(14, 0, true, false),  // Heavy Weapon
            BuildRulesetItemCategoryRule(19, 0, true, false),  // Battle Rifle
            BuildRulesetItemCategoryRule(102, 1, true, false)  // Infantry Weapons (AI Mana Turrets)
        };
    }

    private static RulesetItemRule[] GetDefaultItemRules()
    {
        return new[]
        {
            // One-Hit Knives
            BuildRulesetItemRule(271, 2, 0, true),     // Carver
            BuildRulesetItemRule(285, 2, 0, true),     // Ripper
            BuildRulesetItemRule(286, 2, 0, true),     // Lumine Edge
            BuildRulesetItemRule(6005453, 2, 0, true), // Carver AE
            BuildRulesetItemRule(6005452, 2, 0, true), // Ripper AE
            BuildRulesetItemRule(6005451, 2, 0, true), // Lumine Edge AE
            BuildRulesetItemRule(6009600, 2, 0, true), // NS Firebug

            // Directive Rewards
            BuildRulesetItemRule(800623, 18, 0, true),   // C-4 ARX
            BuildRulesetItemRule(77822, 7, 0, true),     // Gauss Prime
            BuildRulesetItemRule(1909, 7, 0, true),      // Darkstar
            BuildRulesetItemRule(1904, 7, 0, true),      // T1A Unity
            BuildRulesetItemRule(1919, 8, 0, true),      // Eclipse VE3A
            BuildRulesetItemRule(1869, 8, 0, true),      // 19A Fortuna
            BuildRulesetItemRule(1914, 8, 0, true),      // TRAC-Shot
            BuildRulesetItemRule(6005967, 157, 0, true), // NSX-A Kuwa
            BuildRulesetItemRule(6009583, 17, 0, true),  // Infernal Grenade
            BuildRulesetItemRule(6003418, 17, 0, true),  // NSX Fujin
            BuildRulesetItemRule(802025, 2, 0, true),    // Auraxium Slasher
            BuildRulesetItemRule(800626, 2, 0, true),    // Auraxium Force-Blade
            BuildRulesetItemRule(800624, 2, 0, true),    // Auraxium Mag-Cutter
            BuildRulesetItemRule(800625, 2, 0, true),    // Auraxium Chainblade
            BuildRulesetItemRule(803699, 6, 0, true),    // NS-15 Gallows (Bounty Directive)
            BuildRulesetItemRule(1894, 6, 0, true),      // Betelgeuse 54-A
            BuildRulesetItemRule(1879, 6, 0, true),      // NC6A GODSAW
            BuildRulesetItemRule(1924, 6, 0, true),      // T9A "Butcher"
            BuildRulesetItemRule(6005969, 3, 0, true),   // NSX-A Yawara (NSX Pistol)
            BuildRulesetItemRule(1959, 3, 0, true),      // The Immortal
            BuildRulesetItemRule(1889, 3, 0, true),      // The Executive
            BuildRulesetItemRule(1954, 3, 0, true),      // The President
            BuildRulesetItemRule(1964, 13, 0, true),     // The Kraken
            BuildRulesetItemRule(1939, 4, 0, true),      // Chaos
            BuildRulesetItemRule(1884, 4, 0, true),      // The Brawler
            BuildRulesetItemRule(1934, 4, 0, true),      // Havoc
            BuildRulesetItemRule(6005968, 5, 0, true),   // NSX-A Kappa
            BuildRulesetItemRule(1949, 5, 0, true),      // Skorpios
            BuildRulesetItemRule(1899, 5, 0, true),      // Tempest
            BuildRulesetItemRule(1944, 5, 0, true),      // Shuriken
            BuildRulesetItemRule(1979, 11, 0, true),     // Parsec VX3-A
            BuildRulesetItemRule(1969, 11, 0, true),     // The Moonshot
            BuildRulesetItemRule(1974, 11, 0, true),     // Bighorn .50M

            // Semi-Auto Snipers
            BuildRulesetItemRule(6008652, 11, 0, true), // NSX "Ivory" Daimyo
            BuildRulesetItemRule(6008670, 11, 0, true), // NSX "Networked" Daimyo
            BuildRulesetItemRule(804255, 11, 0, true),  // NSX Daimyo
            BuildRulesetItemRule(26002, 11, 0, true),   // Phantom VA23
            BuildRulesetItemRule(7337, 11, 0, true),    // Phaseshift VX-S
            BuildRulesetItemRule(89, 11, 0, true),      // VA39 Spectre
            BuildRulesetItemRule(24000, 11, 0, true),   // Gauss SPR
            BuildRulesetItemRule(24002, 11, 0, true),   // Impetus
            BuildRulesetItemRule(88, 11, 0, true),      // 99SV
            BuildRulesetItemRule(25002, 11, 0, true),   // KSR-35

            // Gen-1 SMGs
            BuildRulesetItemRule(29000, 5, 0, true),    // Eridani SX5
            BuildRulesetItemRule(6002772, 5, 0, true),  // Eridani SX5-AE
            BuildRulesetItemRule(29005, 5, 0, true),    // Eridani SX5G
            BuildRulesetItemRule(27000, 5, 0, true),    // AF-4 Cyclone
            BuildRulesetItemRule(6002824, 5, 0, true),  // AF-4AE Cyclone
            BuildRulesetItemRule(27005, 5, 0, true),    // AF-4G Cyclone
            BuildRulesetItemRule(28000, 5, 0, true),    // SMG-46 Armistice
            BuildRulesetItemRule(6002800, 5, 0, true),  // SMG-46AE Armistice
            BuildRulesetItemRule(28005, 5, 0, true),    // SMG-46G Armistice

            // Gen-3 SMGs
            BuildRulesetItemRule(6003925, 5, 0, true), // VE-S Canis
            BuildRulesetItemRule(6003850, 5, 0, true), // MGR-S1 Gladius
            BuildRulesetItemRule(6003879, 5, 0, true), // MG-S1 Jackal

            // Anti-Personnel Explosives
            BuildRulesetItemRule(650, 18, 0, true),     // Tank Mine
            BuildRulesetItemRule(6005961, 18, 0, true), // Tank Mine
            BuildRulesetItemRule(6005962, 18, 0, true), // Tank Mine
            BuildRulesetItemRule(1045, 18, 0, true),    // Proximity Mine
            BuildRulesetItemRule(1044, 18, 0, true),    // Bouncing Betty
            BuildRulesetItemRule(429, 18, 0, true),     // Claymore
            BuildRulesetItemRule(6005243, 18, 0, true), // F.U.S.E. (Anti-Infantry)
            BuildRulesetItemRule(6005963, 18, 0, true), // Proximity Mine
            BuildRulesetItemRule(6005422, 18, 0, true), // Proximity Mine

            // A7 Weapons
            BuildRulesetItemRule(6003943, 3, 0, true),  // NS-357 IA
            BuildRulesetItemRule(6003793, 3, 0, true),  // NS-44L Showdown
            BuildRulesetItemRule(6004992, 11, 0, true), // NS-AM8 Shortbow

            // Campaign Reward Weapons
            BuildRulesetItemRule(6009524, 17, 0, true), // Condensate Grenade
            BuildRulesetItemRule(6009515, 2, 0, true),  // NS Icebreaker
            BuildRulesetItemRule(6009516, 2, 0, true),  // NS Icebreaker
            BuildRulesetItemRule(6009517, 2, 0, true),  // NS Icebreaker
            BuildRulesetItemRule(6009518, 2, 0, true),  // NS Icebreaker
            BuildRulesetItemRule(6009463, 2, 0, true),  // NS Icebreaker

            // Misc. Weapons
            BuildRulesetItemRule(6050, 17, 0, true),    // Decoy Grenade
            BuildRulesetItemRule(6004750, 17, 0, true), // Flamewake Grenade
            BuildRulesetItemRule(6009459, 17, 0, true), // Lightning Grenade
            BuildRulesetItemRule(6005472, 17, 0, true), // NSX Raijin
            BuildRulesetItemRule(6005304, 17, 0, true), // Smoke Grenade
            BuildRulesetItemRule(882, 17, 0, true),     // Sticky Grenade
            BuildRulesetItemRule(880, 17, 0, true),     // Sticky Grenade
            BuildRulesetItemRule(881, 17, 0, true),     // Sticky Grenade
            BuildRulesetItemRule(6005328, 17, 0, true), // Sticky Grenade
            BuildRulesetItemRule(804795, 2, 0, true),   // NSX Amaterasu

            // NSX Weapons
            BuildRulesetItemRule(44705, 17, 0, true),  // Plasma Grenade (NSX Defector Grenade Printer)
            BuildRulesetItemRule(6008687, 2, 0, true), // Defector Claws

            // Proposed Bans
            BuildRulesetItemRule(75490, 3, 0, true),  // NS Patriot Flare Gun
            BuildRulesetItemRule(75521, 3, 0, true),  // VS Patriot Flare Gun
            BuildRulesetItemRule(803009, 3, 0, true), // VS Triumph Flare Gun
            BuildRulesetItemRule(75517, 3, 0, true),  // NC Patriot Flare Gun
            BuildRulesetItemRule(803007, 3, 0, true), // NC Triumph Flare Gun
            BuildRulesetItemRule(75519, 3, 0, true),  // TR Patriot Flare Gun
            BuildRulesetItemRule(803008, 3, 0, true)  // TR Triumph Flare Gun
        };
    }

    private static RulesetActionRule[] GetDefaultActionRules()
    {
        // MaxKillInfantry & MaxKillMax are worth 0 points
        return new[]
        {
            BuildRulesetActionRule(ScrimActionType.FirstBaseCapture, 9), // PIL 1: 18
            BuildRulesetActionRule(ScrimActionType.SubsequentBaseCapture, 18), // PIL 1: 36
            BuildRulesetActionRule(ScrimActionType.InfantryKillMax, 6), // PIL 1: -12
            BuildRulesetActionRule(ScrimActionType.InfantryTeamkillInfantry, -2), // PIL 1: -3
            BuildRulesetActionRule(ScrimActionType.InfantryTeamkillMax, -8), // PIL 1: -15
            BuildRulesetActionRule(ScrimActionType.InfantrySuicide, -2), // PIL 1: -3
            BuildRulesetActionRule(ScrimActionType.MaxTeamkillMax, -8), // PIL 1: -15
            BuildRulesetActionRule(ScrimActionType.MaxTeamkillInfantry, -2), // PIL 1: -3
            BuildRulesetActionRule(ScrimActionType.MaxSuicide, -8), // PIL 1: -12
            BuildRulesetActionRule(ScrimActionType.MaxKillInfantry, 0), // PIL 1: 0
            BuildRulesetActionRule(ScrimActionType.MaxKillMax, 0), // PIL 1: 0
            BuildRulesetActionRule(ScrimActionType.InfantryKillInfantry, 0, true) // PIL 1: 0
        };
    }

    private static RulesetFacilityRule[] GetDefaultFacilityRules()
    {
        return new[]
        {
            /* Hossin */
            BuildRulesetFacilityRule(266000, 4106), // Kessel's Crossing
            BuildRulesetFacilityRule(272000, 4112), // Bridgewater Shipping
            BuildRulesetFacilityRule(283000, 4123), // Nettlemire
            BuildRulesetFacilityRule(286000, 4126), // Four Fingers
            BuildRulesetFacilityRule(287070, 4266), // Fort Liberty
            BuildRulesetFacilityRule(302030, 4173), // Acan South
            BuildRulesetFacilityRule(303030, 4183), // Bitol Eastern
            BuildRulesetFacilityRule(305010, 4201), // Ghanan South
            BuildRulesetFacilityRule(307010, 4221), // Chac Fusion

            /* Esamir */
            BuildRulesetFacilityRule(239000, 18010), // Pale Canyon
            BuildRulesetFacilityRule(244610, 18067), // Rime Analtyics
            BuildRulesetFacilityRule(244620, 18068), // The Rink
            BuildRulesetFacilityRule(252020, 18050), // Elli Barracks
            BuildRulesetFacilityRule(254010, 18055), // Eisa Mountain Pass

            /* Indar */
            BuildRulesetFacilityRule(219, 2420), // Ceres
            BuildRulesetFacilityRule(230, 2431), // Xenotech
            BuildRulesetFacilityRule(3430, 2456), // Peris Eastern
            BuildRulesetFacilityRule(3620, 2466), // Rashnu

            /* Amerish */
            BuildRulesetFacilityRule(210002, 6357) // Wokuk Shipping
        };
    }

    private static RulesetActionRule BuildRulesetActionRule(int rulesetId, ScrimActionType actionType, int points = 0, bool deferToItemCategoryRules = false)
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

    private static RulesetActionRule BuildRulesetActionRule
    (
        ScrimActionType actionType,
        int points,
        bool deferToItemCategoryRules = false
    )
    {
        return new RulesetActionRule
        {
            ScrimActionType = actionType,
            Points = points,
            DeferToItemCategoryRules = deferToItemCategoryRules,
            ScrimActionTypeDomain = ScrimAction.GetDomainFromActionType(actionType)
        };
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

    private static RulesetItemCategoryRule BuildRulesetItemCategoryRule
    (
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

    private static RulesetItemRule BuildRulesetItemRule
    (
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

    private static RulesetFacilityRule BuildRulesetFacilityRule(uint facilityId, uint mapRegionId)
        => new()
        {
            FacilityId = facilityId,
            MapRegionId = mapRegionId
        };

    public async Task SeedScrimActionModelsAsync(CancellationToken ct = default)
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        List<ScrimAction> createdEntities = new();
        List<ScrimActionType> allActionTypeValues = new(Enum.GetValues<ScrimActionType>());
        List<ScrimAction> storeEntities = await dbContext.ScrimActions.ToListAsync(cancellationToken: ct);

        allActionTypeValues.AddRange(storeEntities.Where(a => !allActionTypeValues.Contains(a.Action)).Select(a => a.Action).ToList());

        foreach (ScrimActionType value in allActionTypeValues.Distinct())
        {
            try
            {
                ScrimAction? storeEntity = storeEntities.FirstOrDefault(e => e.Action == value);

                if (storeEntity is null)
                {
                    createdEntities.Add(ConvertToDbModel(value));
                }
                else if (Enum.IsDefined(value))
                {
                    storeEntity = ConvertToDbModel(value);
                    dbContext.ScrimActions.Update(storeEntity);
                }
                else
                {
                    dbContext.ScrimActions.Remove(storeEntity);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed a scrim action model");
            }
        }

        if (createdEntities.Any())
            dbContext.ScrimActions.AddRange(createdEntities);

        await dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Seeded Scrim Actions store");
    }

    private static ScrimAction ConvertToDbModel(ScrimActionType value)
    {
        string? name = Enum.GetName(value);
        name ??= "Unknown action";

        return new ScrimAction
        {
            Action = value,
            Name = name,
            Description = Regex.Replace(name, @"(\p{Ll})(\p{Lu})", "$1 $2"),
            Domain = ScrimAction.GetDomainFromActionType(value)
        };
    }
}
