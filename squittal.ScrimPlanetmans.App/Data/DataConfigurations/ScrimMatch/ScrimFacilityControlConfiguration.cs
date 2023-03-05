﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using squittal.ScrimPlanetmans.App.Data.Models;

namespace squittal.ScrimPlanetmans.App.Data.DataConfigurations.ScrimMatch;

public class ScrimFacilityControlConfiguration : IEntityTypeConfiguration<ScrimFacilityControl>
{
    public void Configure(EntityTypeBuilder<ScrimFacilityControl> builder)
    {
        builder.ToTable("ScrimFacilityControl");

        builder.HasKey(e => new
        {
            e.ScrimMatchId,
            e.Timestamp,
            e.FacilityId,
            e.ControllingTeamOrdinal
        });

        builder.Property(e => e.Points).HasDefaultValue(0);

        builder.Ignore(e => e.ScrimMatch);
        builder.Ignore(e => e.ControllingFaction);
        builder.Ignore(e => e.Zone);
        builder.Ignore(e => e.World);
    }
}
