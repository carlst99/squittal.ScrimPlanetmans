using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using squittal.ScrimPlanetmans.App.Data.Models;

namespace squittal.ScrimPlanetmans.App.Data.DataConfigurations.ScrimMatch;

public class ScrimGrenadeAssistConfiguration : IEntityTypeConfiguration<ScrimGrenadeAssist>
{
    public void Configure(EntityTypeBuilder<ScrimGrenadeAssist> builder)
    {
        builder.ToTable("ScrimGrenadeAssist");

        builder.HasKey(e => new
        {
            e.ScrimMatchId,
            e.Timestamp,
            e.AttackerCharacterId,
            e.VictimCharacterId
        });

        builder.Property(e => e.Points).HasDefaultValue(0);
    }
}
