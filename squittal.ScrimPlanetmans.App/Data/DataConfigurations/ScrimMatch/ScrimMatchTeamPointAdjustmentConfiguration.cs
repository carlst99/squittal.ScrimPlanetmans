﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using squittal.ScrimPlanetmans.App.Data.Models;
using squittal.ScrimPlanetmans.App.Models;

namespace squittal.ScrimPlanetmans.App.Data.DataConfigurations.ScrimMatch;

public class ScrimMatchTeamPointAdjustmentConfiguration : IEntityTypeConfiguration<ScrimMatchTeamPointAdjustment>
{
    public void Configure(EntityTypeBuilder<ScrimMatchTeamPointAdjustment> builder)
    {
        builder.ToTable("ScrimMatchTeamPointAdjustment");

        builder.HasKey(e => new
        {
            e.ScrimMatchId,
            e.TeamOrdinal,
            e.Timestamp
        });

        builder.HasOne(teamAdjustment => teamAdjustment.ScrimMatchTeamResult)
            .WithMany(teamResult => teamResult.PointAdjustments)
            .HasForeignKey(teamAdjustment => new { teamAdjustment.ScrimMatchId, teamAdjustment.TeamOrdinal });

        builder.Property(e => e.Points).HasDefaultValue(0);
        builder.Property(e => e.TeamOrdinal).HasConversion
        (
            p => (int)p,
            p => (TeamDefinition)p
        );
    }
}
