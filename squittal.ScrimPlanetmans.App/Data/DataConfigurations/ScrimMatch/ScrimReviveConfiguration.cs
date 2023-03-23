using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using squittal.ScrimPlanetmans.App.Data.Models;

namespace squittal.ScrimPlanetmans.App.Data.DataConfigurations.ScrimMatch;

public class ScrimReviveConfiguration : IEntityTypeConfiguration<ScrimRevive>
{
    public void Configure(EntityTypeBuilder<ScrimRevive> builder)
    {
        builder.ToTable("ScrimRevive");

        builder.HasKey(e => new
        {
            e.ScrimMatchId,
            e.Timestamp,
            e.MedicCharacterId,
            e.RevivedCharacterId
        });

        builder.Property(e => e.ExperienceGainAmount).HasDefaultValue(0);
        builder.Property(e => e.Points).HasDefaultValue(0);

        builder.Ignore(e => e.ScrimMatch);
        builder.Ignore(e => e.MedicParticipatingPlayer);
        builder.Ignore(e => e.RevivedParticipatingPlayer);
    }
}
