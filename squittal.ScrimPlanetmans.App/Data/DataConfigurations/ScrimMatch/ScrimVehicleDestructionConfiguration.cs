using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using squittal.ScrimPlanetmans.App.Data.Models;

namespace squittal.ScrimPlanetmans.App.Data.DataConfigurations.ScrimMatch;

public class ScrimVehicleDestructionConfiguration : IEntityTypeConfiguration<ScrimVehicleDestruction>
{
    public void Configure(EntityTypeBuilder<ScrimVehicleDestruction> builder)
    {
        builder.ToTable("ScrimVehicleDestruction");

        builder.HasKey(e => new
        {
            e.ScrimMatchId,
            e.Timestamp,
            e.AttackerCharacterId,
            e.VictimCharacterId,
            e.VictimVehicleId
        });

        builder.Property(e => e.ScrimMatchRound).HasDefaultValue(-1);
        builder.Property(e => e.Points).HasDefaultValue(0);
    }
}
