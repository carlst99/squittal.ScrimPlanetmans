using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using squittal.ScrimPlanetmans.App.Data.Models;

namespace squittal.ScrimPlanetmans.App.Data.DataConfigurations.ScrimMatch;

public class ScrimDeathConfiguration : IEntityTypeConfiguration<ScrimDeath>
{
    public void Configure(EntityTypeBuilder<ScrimDeath> builder)
    {
        builder.ToTable("ScrimDeath");

        builder.HasKey(e => new
        {
            e.ScrimMatchId,
            e.Timestamp,
            e.AttackerCharacterId,
            e.VictimCharacterId
        });

        builder.Property(e => e.ScrimMatchRound).HasDefaultValue(-1);
    }
}
