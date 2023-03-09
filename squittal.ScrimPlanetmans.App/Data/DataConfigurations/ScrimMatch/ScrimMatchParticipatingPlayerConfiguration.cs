using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using squittal.ScrimPlanetmans.App.Data.Models;
using squittal.ScrimPlanetmans.App.Models;

namespace squittal.ScrimPlanetmans.App.Data.DataConfigurations.ScrimMatch;

public class ScrimMatchParticipatingPlayerConfiguration : IEntityTypeConfiguration<ScrimMatchParticipatingPlayer>
{
    public void Configure(EntityTypeBuilder<ScrimMatchParticipatingPlayer> builder)
    {
        builder.ToTable("ScrimMatchParticipatingPlayer");

        builder.HasKey(e => new
        {
            e.ScrimMatchId,
            e.CharacterId
        });

        builder.Property(e => e.IsFromOutfit).HasDefaultValue(false);
        builder.Property(e => e.IsFromConstructedTeam).HasDefaultValue(false);
        builder.Property(e => e.TeamOrdinal).HasConversion
        (
            p => (int)p,
            p => (TeamDefinition)p
        );
    }
}
