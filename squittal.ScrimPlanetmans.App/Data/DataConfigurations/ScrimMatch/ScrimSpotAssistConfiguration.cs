using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using squittal.ScrimPlanetmans.App.Data.Models;

namespace squittal.ScrimPlanetmans.App.Data.DataConfigurations.ScrimMatch;

public class ScrimSpotAssistConfiguration : IEntityTypeConfiguration<ScrimSpotAssist>
{
    public void Configure(EntityTypeBuilder<ScrimSpotAssist> builder)
    {
        builder.ToTable("ScrimSpotAssist");

        builder.HasKey(e => new
        {
            e.ScrimMatchId,
            e.Timestamp,
            e.SpotterCharacterId,
            e.VictimCharacterId
        });

        builder.Property(e => e.ExperienceGainAmount).HasDefaultValue(0);
        builder.Property(e => e.Points).HasDefaultValue(0);

        builder.Ignore(e => e.ScrimMatch);
        builder.Ignore(e => e.SpotterParticipatingPlayer);
        builder.Ignore(e => e.VictimParticipatingPlayer);
        builder.Ignore(e => e.World);
        builder.Ignore(e => e.Zone);
    }
}
