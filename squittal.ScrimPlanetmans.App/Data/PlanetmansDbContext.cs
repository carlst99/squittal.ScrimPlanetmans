﻿using Microsoft.EntityFrameworkCore;
using squittal.ScrimPlanetmans.App.Data.DataConfigurations.ScrimMatch;
using squittal.ScrimPlanetmans.App.Data.DataConfigurations.ScrimMatchReports;
using squittal.ScrimPlanetmans.App.Data.Models;
using squittal.ScrimPlanetmans.App.Models.ScrimMatchReports;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.Data;

public class PlanetmansDbContext : DbContext
{
    #region Scrim Match DbSets
    public DbSet<Ruleset> Rulesets { get; set; }
    public DbSet<RulesetOverlayConfiguration> RulesetOverlayConfigurations { get; set; }
    public DbSet<RulesetActionRule> RulesetActionRules { get; set; }
    public DbSet<RulesetItemCategoryRule> RulesetItemCategoryRules { get; set; }
    public DbSet<RulesetItemRule> RulesetItemRules { get; set; }
    public DbSet<RulesetFacilityRule> RulesetFacilityRules { get; set; }

    public DbSet<Models.ScrimMatch> ScrimMatches { get; set; }
    public DbSet<ScrimMatchRoundConfiguration> ScrimMatchRoundConfigurations { get; set; }
    public DbSet<ScrimMatchParticipatingPlayer> ScrimMatchParticipatingPlayers { get; set; }

    public DbSet<ScrimMatchTeamResult> ScrimMatchTeamResults { get; set; }
    public DbSet<ScrimMatchTeamPointAdjustment> ScrimMatchTeamPointAdjustments { get; set; }

    public DbSet<ScrimDeath> ScrimDeaths { get; set; }
    public DbSet<ScrimVehicleDestruction> ScrimVehicleDestructions { get; set; }
    public DbSet<ScrimDamageAssist> ScrimDamageAssists { get; set; }
    public DbSet<ScrimGrenadeAssist> ScrimGrenadeAssists { get; set; }
    public DbSet<ScrimSpotAssist> ScrimSpotAssists { get; set; }
    public DbSet<ScrimRevive> ScrimRevives { get; set; }
    public DbSet<ScrimFacilityControl> ScrimFacilityControls { get; set; }

    public DbSet<ConstructedTeam> ConstructedTeams { get; set; }
    public DbSet<ConstructedTeamPlayerMembership> ConstructedTeamPlayerMemberships { get; set; }
    //public DbSet<ConstructedTeamFactionPreference> ConstructedTeamFactionPreferences { get; set; }
    #endregion

    #region Views
    public DbSet<ScrimMatchInfo> ScrimMatchInfo { get; set; }
    public DbSet<ScrimMatchReportInfantryPlayerStats> ScrimMatchReportInfantryPlayerStats { get; set; }
    public DbSet<ScrimMatchReportInfantryPlayerRoundStats> ScrimMatchReportInfantryPlayerRoundStats { get; set; }
    public DbSet<ScrimMatchReportInfantryTeamStats> ScrimMatchReportInfantryTeamStats { get; set; }
    public DbSet<ScrimMatchReportInfantryTeamRoundStats> ScrimMatchReportInfantryTeamRoundStats { get; set; }
    public DbSet<ScrimMatchReportInfantryDeath> ScrimMatchReportInfantryDeaths { get; set; }
    public DbSet<ScrimMatchReportInfantryPlayerHeadToHeadStats> ScrimMatchReportInfantryPlayerHeadToHeadStats { get; set; }
    public DbSet<ScrimMatchReportInfantryPlayerClassEventCounts> ScrimMatchReportInfantryPlayerClassEventCounts { get; set; }
    public DbSet<ScrimMatchReportInfantryPlayerWeaponStats> ScrimMatchReportInfantryPlayerWeaponStats { get; set; }
    #endregion Views

    public PlanetmansDbContext(DbContextOptions<PlanetmansDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        #region Scrim Match DbSets
        builder.ApplyConfiguration(new RulesetConfiguration());
        builder.ApplyConfiguration(new RulesetOverlayConfigurationConfiguration());
        builder.ApplyConfiguration(new RulesetActionRuleConfiguration());
        builder.ApplyConfiguration(new RulesetItemCategoryRuleConfiguration());
        builder.ApplyConfiguration(new RulesetItemRuleConfiguration());
        builder.ApplyConfiguration(new RulesetFacilityRuleConfiguration());

        builder.ApplyConfiguration(new ScrimMatchConfiguration());
        builder.ApplyConfiguration(new ScrimMatchRoundConfigurationConfiguration());
        builder.ApplyConfiguration(new ScrimMatchParticipatingPlayerConfiguration());

        builder.ApplyConfiguration(new ScrimMatchTeamResultConfiguration());
        builder.ApplyConfiguration(new ScrimMatchTeamPointAdjustmentConfiguration());

        builder.ApplyConfiguration(new ScrimDeathConfiguration());
        builder.ApplyConfiguration(new ScrimVehicleDestructionConfiguration());

        builder.ApplyConfiguration(new ScrimDamageAssistConfiguration());
        builder.ApplyConfiguration(new ScrimGrenadeAssistConfiguration());
        builder.ApplyConfiguration(new ScrimSpotAssistConfiguration());
        builder.ApplyConfiguration(new ScrimReviveConfiguration());
        builder.ApplyConfiguration(new ScrimFacilityControlConfiguration());

        builder.ApplyConfiguration(new ConstructedTeamConfiguration());
        builder.ApplyConfiguration(new ConstructedTeamPlayerMembershipConfiguration());
        //builder.ApplyConfiguration(new ConstructedTeamFactionPreferenceConfiguration());
        #endregion

        #region Views
        builder.ApplyConfiguration(new ScrimMatchInfoConfiguration());
        builder.ApplyConfiguration(new ScrimMatchReportInfantryPlayerStatsConfiguration());
        builder.ApplyConfiguration(new ScrimMatchReportInfantryPlayerRoundStatsConfiguration());
        builder.ApplyConfiguration(new ScrimMatchReportInfantryTeamStatsConfiguration());
        builder.ApplyConfiguration(new ScrimMatchReportInfantryTeamRoundStatsConfiguration());
        builder.ApplyConfiguration(new ScrimMatchReportInfantryDeathConfiguration());
        builder.ApplyConfiguration(new ScrimMatchReportInfantryPlayerHeadToHeadStatsConfiguration());
        builder.ApplyConfiguration(new ScrimMatchReportInfantryPlayerClassEventCountsConfiguration());
        builder.ApplyConfiguration(new ScrimMatchReportInfantryPlayerWeaponStatsConfiguration());
        #endregion Views
    }
}
