using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using squittal.ScrimPlanetmans.App.Data.Models;
using squittal.ScrimPlanetmans.App.Models;

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
        builder.Property(e => e.ControllingTeamOrdinal).HasConversion
        (
            t => (int)t,
            t => (TeamDefinition)t
        );
    }
}
