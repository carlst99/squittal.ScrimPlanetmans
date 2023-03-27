using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusRest;
using squittal.ScrimPlanetmans.App.Abstractions.Services.Planetside;
using squittal.ScrimPlanetmans.App.Abstractions.Services.Rulesets;
using squittal.ScrimPlanetmans.App.Abstractions.Services.ScrimMatch;
using squittal.ScrimPlanetmans.App.Data;
using squittal.ScrimPlanetmans.App.Extensions;
using squittal.ScrimPlanetmans.App.Models.CensusRest;
using squittal.ScrimPlanetmans.App.ScrimMatch.Events;
using squittal.ScrimPlanetmans.App.ScrimMatch.Interfaces;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset;

public class ScrimRulesetManager : IScrimRulesetManager
{
    private const int DEFAULT_RULESET_ID = 1;

    private readonly ILogger<ScrimRulesetManager> _logger;
    private readonly IDbContextFactory<PlanetmansDbContext> _dbContextFactory;
    private readonly IItemCategoryService _itemCategoryService;
    private readonly ICensusItemService _itemService;
    private readonly IRulesetDataService _rulesetDataService;
    private readonly IScrimMessageBroadcastService _messageService;

    public Models.Ruleset? ActiveRuleset { get; private set; }
    private readonly AutoResetEvent _activateRulesetAutoEvent = new(true);

    public ScrimRulesetManager
    (
        IDbContextFactory<PlanetmansDbContext> dbContextFactory,
        IItemCategoryService itemCategoryService,
        ICensusItemService itemService,
        IRulesetDataService rulesetDataService,
        IScrimMessageBroadcastService messageService,
        ILogger<ScrimRulesetManager> logger
    )
    {
        _dbContextFactory = dbContextFactory;
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
        await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

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
        await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

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
        Stopwatch stopWatchTotal = Stopwatch.StartNew();

        await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        bool rulesetExistsInDb = true;
        Models.Ruleset? storeRuleset = await _rulesetDataService.GetRulesetFromIdAsync
        (
            DEFAULT_RULESET_ID,
            ct
        );

        if (storeRuleset is null)
        {
            storeRuleset = new Models.Ruleset
            {
                Name = "Default",
                DefaultMatchTitle = "PS2 Scrim",
                DateCreated = DateTime.UtcNow,
                IsDefault = true,
                DefaultEndRoundOnFacilityCapture = false
            };

            rulesetExistsInDb = false;
        }

        storeRuleset.RulesetActionRules ??= new List<RulesetActionRule>();
        storeRuleset.RulesetItemCategoryRules ??= new List<RulesetItemCategoryRule>();
        storeRuleset.RulesetItemRules ??= new List<RulesetItemRule>();
        storeRuleset.RulesetFacilityRules ??= new List<RulesetFacilityRule>();

        // Setup overlay if required

        if (storeRuleset.RulesetOverlayConfiguration is null)
        {
            storeRuleset.RulesetOverlayConfiguration = new RulesetOverlayConfiguration
            {
                RulesetId = DEFAULT_RULESET_ID,
                UseCompactLayout = false,
                StatsDisplayType = OverlayStatsDisplayType.InfantryScores,
                ShowStatusPanelScores = null
            };

            dbContext.RulesetOverlayConfigurations.Add(storeRuleset.RulesetOverlayConfiguration);
        }

        // Add new action rules, remove old ones

        Dictionary<ScrimActionType, RulesetActionRule?> knownActionRules = Enum.GetValues<ScrimActionType>()
            .Where(t => t is not (ScrimActionType.None or ScrimActionType.Login or ScrimActionType.Logout))
            .ToDictionary(t => t, _ => (RulesetActionRule?)null);

        foreach (RulesetActionRule rule in GetDefaultActionRules())
        {
            // Ensure that the action type exists, the defaults could be outdated
            if (knownActionRules.ContainsKey(rule.ScrimActionType))
                knownActionRules[rule.ScrimActionType] = rule;
        }

        foreach (RulesetActionRule dbActionRule in storeRuleset.RulesetActionRules)
        {
            bool removed = knownActionRules.Remove(dbActionRule.ScrimActionType);
            if (!removed)
                dbContext.RulesetActionRules.Remove(dbActionRule);
        }

        foreach ((ScrimActionType action, RulesetActionRule? rule) in knownActionRules)
        {
            RulesetActionRule toInsert = rule ?? BuildDefaultActionRule(action, 0, true);
            dbContext.RulesetActionRules.Add(toInsert);
        }

        // Add new item category rules, remove old ones

        IEnumerable<uint>? allWeaponItemCategoryIds = await _itemCategoryService.GetWeaponItemCategoryIdsAsync(ct);

        if (allWeaponItemCategoryIds is not null)
        {
            Dictionary<uint, RulesetItemCategoryRule?> knownItemCategoryRules = allWeaponItemCategoryIds
                .ToDictionary(x => x, _ => (RulesetItemCategoryRule?)null);

            foreach (RulesetItemCategoryRule defaultCategoryRule in GetDefaultItemCategoryRules())
            {
                // Ensure that the category exists, the defaults could be outdated
                if (knownItemCategoryRules.ContainsKey(defaultCategoryRule.ItemCategoryId))
                    knownItemCategoryRules[defaultCategoryRule.ItemCategoryId] = defaultCategoryRule;
            }

            foreach (RulesetItemCategoryRule dbItemCategoryRule in storeRuleset.RulesetItemCategoryRules)
            {
                bool removed = knownItemCategoryRules.Remove(dbItemCategoryRule.ItemCategoryId);
                if (!removed)
                    dbContext.RulesetItemCategoryRules.Remove(dbItemCategoryRule);
            }

            foreach ((uint itemCategory, RulesetItemCategoryRule? rule) in knownItemCategoryRules)
            {
                RulesetItemCategoryRule toInsert = rule
                    ?? BuildDefaultItemCategoryRule(itemCategory, deferToItemRules: true);

                dbContext.RulesetItemCategoryRules.Add(toInsert);
                storeRuleset.RulesetItemCategoryRules.Add(toInsert);
            }
        }

        // Add new item rules, remove old ones

        IReadOnlyList<CensusItem>? allWeaponItems = await _itemService.GetAllWeaponsAsync(ct);

        if (allWeaponItems is not null)
        {
            Dictionary<uint, RulesetItemRule> knownItemRules = allWeaponItems
                .ToDictionary
                (
                    x => x.ItemId,
                    x => BuildDefaultItemRule
                    (
                        x.ItemId,
                        x.ItemCategoryId,
                        storeRuleset.RulesetItemCategoryRules
                            .FirstOrDefault(c => c.ItemCategoryId == x.ItemCategoryId)?.Points ?? 0
                    )
                );

            foreach (RulesetItemRule defaultItemRule in GetDefaultItemRules())
            {
                // Ensure that the item exists, the defaults could be outdated
                if (knownItemRules.ContainsKey(defaultItemRule.ItemId))
                    knownItemRules[defaultItemRule.ItemId] = defaultItemRule;
            }

            foreach (RulesetItemRule dbItemRule in storeRuleset.RulesetItemRules)
            {
                bool removed = knownItemRules.Remove(dbItemRule.ItemId);
                if (!removed)
                    dbContext.RulesetItemRules.Remove(dbItemRule);
            }

            foreach (RulesetItemRule itemRule in knownItemRules.Values)
                dbContext.RulesetItemRules.Add(itemRule);
        }

        // Add new facility rules, remove old ones

        Dictionary<uint, RulesetFacilityRule> knownFacilityRules = GetDefaultFacilityRules()
            .ToDictionary(x => x.FacilityId, x => x);

        foreach (RulesetFacilityRule dbFacilityRule in storeRuleset.RulesetFacilityRules)
        {
            bool removed = knownFacilityRules.Remove(dbFacilityRule.FacilityId);
            if (!removed)
                dbContext.RulesetFacilityRules.Remove(dbFacilityRule);
        }

        foreach (RulesetFacilityRule facilityRule in knownFacilityRules.Values)
            dbContext.RulesetFacilityRules.Add(facilityRule);

        if (rulesetExistsInDb)
            dbContext.Rulesets.Update(storeRuleset);
        else
            dbContext.Rulesets.Add(storeRuleset);

        await dbContext.SaveChangesAsync(ct);

        stopWatchTotal.Stop();
        _logger.LogInformation
        (
            "Finished seeding default ruleset (elapsed: {TotalTime})",
            stopWatchTotal.Elapsed
        );
    }

    private static RulesetItemCategoryRule[] GetDefaultItemCategoryRules()
    {
        return new[]
        {
            BuildDefaultItemCategoryRule(2, 1, false, true),    // Knife
            BuildDefaultItemCategoryRule(3, 1, false, true),    // Pistol
            BuildDefaultItemCategoryRule(4, 1, false, true),    // Shotgun
            BuildDefaultItemCategoryRule(5, 1, false, true),    // SMG
            BuildDefaultItemCategoryRule(6, 1, false, true),    // LMG
            BuildDefaultItemCategoryRule(7, 1, false, true),    // Assault Rifle
            BuildDefaultItemCategoryRule(8, 1, false, true),    // Carbine
            BuildDefaultItemCategoryRule(11, 1, false, true),   // Sniper Rifle
            BuildDefaultItemCategoryRule(13, 0, false, true),   // Rocket Launcher
            BuildDefaultItemCategoryRule(24, 1, false, false),  // Crossbow
            BuildDefaultItemCategoryRule(100, 1, false, false), // Infantry (Nothing)
            BuildDefaultItemCategoryRule(157, 1, false, true),  // Hybrid Rifle (NSX-A Kuwa)

            // Universal Bans
            BuildDefaultItemCategoryRule(12, 0, true, false),  // Scout Rifle
            BuildDefaultItemCategoryRule(14, 0, true, false),  // Heavy Weapon
            BuildDefaultItemCategoryRule(19, 0, true, false),  // Battle Rifle
            BuildDefaultItemCategoryRule(102, 1, true, false)  // Infantry Weapons (AI Mana Turrets)
        };
    }

    private static RulesetItemRule[] GetDefaultItemRules()
    {
        return new[]
        {
            // One-Hit Knives
            BuildDefaultItemRule(271, 2, 0, true),     // Carver
            BuildDefaultItemRule(285, 2, 0, true),     // Ripper
            BuildDefaultItemRule(286, 2, 0, true),     // Lumine Edge
            BuildDefaultItemRule(6005453, 2, 0, true), // Carver AE
            BuildDefaultItemRule(6005452, 2, 0, true), // Ripper AE
            BuildDefaultItemRule(6005451, 2, 0, true), // Lumine Edge AE
            BuildDefaultItemRule(6009600, 2, 0, true), // NS Firebug

            // Directive Rewards
            BuildDefaultItemRule(800623, 18, 0, true),   // C-4 ARX
            BuildDefaultItemRule(77822, 7, 0, true),     // Gauss Prime
            BuildDefaultItemRule(1909, 7, 0, true),      // Darkstar
            BuildDefaultItemRule(1904, 7, 0, true),      // T1A Unity
            BuildDefaultItemRule(1919, 8, 0, true),      // Eclipse VE3A
            BuildDefaultItemRule(1869, 8, 0, true),      // 19A Fortuna
            BuildDefaultItemRule(1914, 8, 0, true),      // TRAC-Shot
            BuildDefaultItemRule(6005967, 157, 0, true), // NSX-A Kuwa
            BuildDefaultItemRule(6009583, 17, 0, true),  // Infernal Grenade
            BuildDefaultItemRule(6003418, 17, 0, true),  // NSX Fujin
            BuildDefaultItemRule(802025, 2, 0, true),    // Auraxium Slasher
            BuildDefaultItemRule(800626, 2, 0, true),    // Auraxium Force-Blade
            BuildDefaultItemRule(800624, 2, 0, true),    // Auraxium Mag-Cutter
            BuildDefaultItemRule(800625, 2, 0, true),    // Auraxium Chainblade
            BuildDefaultItemRule(803699, 6, 0, true),    // NS-15 Gallows (Bounty Directive)
            BuildDefaultItemRule(1894, 6, 0, true),      // Betelgeuse 54-A
            BuildDefaultItemRule(1879, 6, 0, true),      // NC6A GODSAW
            BuildDefaultItemRule(1924, 6, 0, true),      // T9A "Butcher"
            BuildDefaultItemRule(6005969, 3, 0, true),   // NSX-A Yawara (NSX Pistol)
            BuildDefaultItemRule(1959, 3, 0, true),      // The Immortal
            BuildDefaultItemRule(1889, 3, 0, true),      // The Executive
            BuildDefaultItemRule(1954, 3, 0, true),      // The President
            BuildDefaultItemRule(1964, 13, 0, true),     // The Kraken
            BuildDefaultItemRule(1939, 4, 0, true),      // Chaos
            BuildDefaultItemRule(1884, 4, 0, true),      // The Brawler
            BuildDefaultItemRule(1934, 4, 0, true),      // Havoc
            BuildDefaultItemRule(6005968, 5, 0, true),   // NSX-A Kappa
            BuildDefaultItemRule(1949, 5, 0, true),      // Skorpios
            BuildDefaultItemRule(1899, 5, 0, true),      // Tempest
            BuildDefaultItemRule(1944, 5, 0, true),      // Shuriken
            BuildDefaultItemRule(1979, 11, 0, true),     // Parsec VX3-A
            BuildDefaultItemRule(1969, 11, 0, true),     // The Moonshot
            BuildDefaultItemRule(1974, 11, 0, true),     // Bighorn .50M

            // Semi-Auto Snipers
            BuildDefaultItemRule(6008652, 11, 0, true), // NSX "Ivory" Daimyo
            BuildDefaultItemRule(6008670, 11, 0, true), // NSX "Networked" Daimyo
            BuildDefaultItemRule(804255, 11, 0, true),  // NSX Daimyo
            BuildDefaultItemRule(26002, 11, 0, true),   // Phantom VA23
            BuildDefaultItemRule(7337, 11, 0, true),    // Phaseshift VX-S
            BuildDefaultItemRule(89, 11, 0, true),      // VA39 Spectre
            BuildDefaultItemRule(24000, 11, 0, true),   // Gauss SPR
            BuildDefaultItemRule(24002, 11, 0, true),   // Impetus
            BuildDefaultItemRule(88, 11, 0, true),      // 99SV
            BuildDefaultItemRule(25002, 11, 0, true),   // KSR-35

            // Gen-1 SMGs
            BuildDefaultItemRule(29000, 5, 0, true),    // Eridani SX5
            BuildDefaultItemRule(6002772, 5, 0, true),  // Eridani SX5-AE
            BuildDefaultItemRule(29005, 5, 0, true),    // Eridani SX5G
            BuildDefaultItemRule(27000, 5, 0, true),    // AF-4 Cyclone
            BuildDefaultItemRule(6002824, 5, 0, true),  // AF-4AE Cyclone
            BuildDefaultItemRule(27005, 5, 0, true),    // AF-4G Cyclone
            BuildDefaultItemRule(28000, 5, 0, true),    // SMG-46 Armistice
            BuildDefaultItemRule(6002800, 5, 0, true),  // SMG-46AE Armistice
            BuildDefaultItemRule(28005, 5, 0, true),    // SMG-46G Armistice

            // Gen-3 SMGs
            BuildDefaultItemRule(6003925, 5, 0, true), // VE-S Canis
            BuildDefaultItemRule(6003850, 5, 0, true), // MGR-S1 Gladius
            BuildDefaultItemRule(6003879, 5, 0, true), // MG-S1 Jackal

            // Anti-Personnel Explosives
            BuildDefaultItemRule(650, 18, 0, true),     // Tank Mine
            BuildDefaultItemRule(6005961, 18, 0, true), // Tank Mine
            BuildDefaultItemRule(6005962, 18, 0, true), // Tank Mine
            BuildDefaultItemRule(1045, 18, 0, true),    // Proximity Mine
            BuildDefaultItemRule(1044, 18, 0, true),    // Bouncing Betty
            BuildDefaultItemRule(429, 18, 0, true),     // Claymore
            BuildDefaultItemRule(6005243, 18, 0, true), // F.U.S.E. (Anti-Infantry)
            BuildDefaultItemRule(6005963, 18, 0, true), // Proximity Mine
            BuildDefaultItemRule(6005422, 18, 0, true), // Proximity Mine

            // A7 Weapons
            BuildDefaultItemRule(6003943, 3, 0, true),  // NS-357 IA
            BuildDefaultItemRule(6003793, 3, 0, true),  // NS-44L Showdown
            BuildDefaultItemRule(6004992, 11, 0, true), // NS-AM8 Shortbow

            // Campaign Reward Weapons
            BuildDefaultItemRule(6009524, 17, 0, true), // Condensate Grenade
            BuildDefaultItemRule(6009515, 2, 0, true),  // NS Icebreaker
            BuildDefaultItemRule(6009516, 2, 0, true),  // NS Icebreaker
            BuildDefaultItemRule(6009517, 2, 0, true),  // NS Icebreaker
            BuildDefaultItemRule(6009518, 2, 0, true),  // NS Icebreaker
            BuildDefaultItemRule(6009463, 2, 0, true),  // NS Icebreaker

            // Misc. Weapons
            BuildDefaultItemRule(6050, 17, 0, true),    // Decoy Grenade
            BuildDefaultItemRule(6004750, 17, 0, true), // Flamewake Grenade
            BuildDefaultItemRule(6009459, 17, 0, true), // Lightning Grenade
            BuildDefaultItemRule(6005472, 17, 0, true), // NSX Raijin
            BuildDefaultItemRule(6005304, 17, 0, true), // Smoke Grenade
            BuildDefaultItemRule(882, 17, 0, true),     // Sticky Grenade
            BuildDefaultItemRule(880, 17, 0, true),     // Sticky Grenade
            BuildDefaultItemRule(881, 17, 0, true),     // Sticky Grenade
            BuildDefaultItemRule(6005328, 17, 0, true), // Sticky Grenade
            BuildDefaultItemRule(804795, 2, 0, true),   // NSX Amaterasu

            // NSX Weapons
            BuildDefaultItemRule(44705, 17, 0, true),  // Plasma Grenade (NSX Defector Grenade Printer)
            BuildDefaultItemRule(6008687, 2, 0, true), // Defector Claws

            // Proposed Bans
            BuildDefaultItemRule(75490, 3, 0, true),  // NS Patriot Flare Gun
            BuildDefaultItemRule(75521, 3, 0, true),  // VS Patriot Flare Gun
            BuildDefaultItemRule(803009, 3, 0, true), // VS Triumph Flare Gun
            BuildDefaultItemRule(75517, 3, 0, true),  // NC Patriot Flare Gun
            BuildDefaultItemRule(803007, 3, 0, true), // NC Triumph Flare Gun
            BuildDefaultItemRule(75519, 3, 0, true),  // TR Patriot Flare Gun
            BuildDefaultItemRule(803008, 3, 0, true)  // TR Triumph Flare Gun
        };
    }

    private static RulesetActionRule[] GetDefaultActionRules()
    {
        // MaxKillInfantry & MaxKillMax are worth 0 points
        return new[]
        {
            BuildDefaultActionRule(ScrimActionType.FirstBaseCapture, 10),
            BuildDefaultActionRule(ScrimActionType.SubsequentBaseCapture, 20),
            BuildDefaultActionRule(ScrimActionType.InfantryKillMax, 6), // PIL 1: -12
            BuildDefaultActionRule(ScrimActionType.InfantryTeamkillInfantry, -2), // PIL 1: -3
            BuildDefaultActionRule(ScrimActionType.InfantryTeamkillMax, -8), // PIL 1: -15
            BuildDefaultActionRule(ScrimActionType.InfantrySuicide, -2), // PIL 1: -3
            BuildDefaultActionRule(ScrimActionType.MaxTeamkillMax, -8), // PIL 1: -15
            BuildDefaultActionRule(ScrimActionType.MaxTeamkillInfantry, -2), // PIL 1: -3
            BuildDefaultActionRule(ScrimActionType.MaxSuicide, -8), // PIL 1: -12
            BuildDefaultActionRule(ScrimActionType.MaxKillInfantry, 0), // PIL 1: 0
            BuildDefaultActionRule(ScrimActionType.MaxKillMax, 0), // PIL 1: 0
            BuildDefaultActionRule(ScrimActionType.InfantryKillInfantry, 0, true) // PIL 1: 0
        };
    }

    private static RulesetFacilityRule[] GetDefaultFacilityRules()
    {
        return new[]
        {
            /* Hossin */
            BuildDefaultFacilityRule(266000), // Kessel's Crossing
            BuildDefaultFacilityRule(272000), // Bridgewater Shipping
            BuildDefaultFacilityRule(283000), // Nettlemire
            BuildDefaultFacilityRule(286000), // Four Fingers
            BuildDefaultFacilityRule(287070), // Fort Liberty
            BuildDefaultFacilityRule(302030), // Acan South
            BuildDefaultFacilityRule(303030), // Bitol Eastern
            BuildDefaultFacilityRule(305010), // Ghanan South
            BuildDefaultFacilityRule(307010), // Chac Fusion

            /* Esamir */
            BuildDefaultFacilityRule(239000), // Pale Canyon
            BuildDefaultFacilityRule(244610), // Rime Analytics
            BuildDefaultFacilityRule(244620), // The Rink

            /* Indar */
            BuildDefaultFacilityRule(219), // Ceres
            BuildDefaultFacilityRule(230), // Xenotech
            BuildDefaultFacilityRule(3430), // Peris Eastern
            BuildDefaultFacilityRule(3620), // Rashnu

            /* Amerish */
            BuildDefaultFacilityRule(210002) // Wokuk Shipping
        };
    }

    private static RulesetActionRule BuildDefaultActionRule
    (
        ScrimActionType actionType,
        int points,
        bool deferToItemCategoryRules = false
    )
    {
        return new RulesetActionRule
        {
            RulesetId = DEFAULT_RULESET_ID,
            ScrimActionType = actionType,
            Points = points,
            DeferToItemCategoryRules = deferToItemCategoryRules,
            ScrimActionTypeDomain = actionType.GetDomain()
        };
    }

    private static RulesetItemCategoryRule BuildDefaultItemCategoryRule
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
            RulesetId = DEFAULT_RULESET_ID,
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

    private static RulesetItemRule BuildDefaultItemRule
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
            RulesetId = DEFAULT_RULESET_ID,
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

    private static RulesetFacilityRule BuildDefaultFacilityRule(uint facilityId)
        => new()
        {
            RulesetId = DEFAULT_RULESET_ID,
            FacilityId = facilityId
        };
}
