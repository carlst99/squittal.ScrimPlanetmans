﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.Data.DataConfigurations.ScrimMatch;

public class RulesetOverlayConfigurationConfiguration : IEntityTypeConfiguration<RulesetOverlayConfiguration>
{
    public void Configure(EntityTypeBuilder<RulesetOverlayConfiguration> builder)
    {
        builder.ToTable("RulesetOverlayConfiguration");

        builder.HasKey(e => new { e.RulesetId });

        builder.Property(e => e.RulesetId).ValueGeneratedNever();

        builder.Ignore(e => e.Ruleset);

        builder.HasOne(e => e.Ruleset)
            .WithOne(r => r.RulesetOverlayConfiguration);

        builder.Property(e => e.UseCompactLayout).HasDefaultValue(false);
        builder.Property(e => e.StatsDisplayType).HasDefaultValue(OverlayStatsDisplayType.InfantryScores);
    }
}
