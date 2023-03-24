using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using squittal.ScrimPlanetmans.App.Data.Models;

namespace squittal.ScrimPlanetmans.App.Data.DataConfigurations.ScrimMatch;

public class ScrimMatchRoundConfigurationConfiguration : IEntityTypeConfiguration<ScrimMatchRoundConfiguration>
{
    public void Configure(EntityTypeBuilder<ScrimMatchRoundConfiguration> builder)
    {
        builder.ToTable("ScrimMatchRoundConfiguration");

        builder.HasKey(e => e.ScrimMatchId);
        builder.HasKey(e => new
        {
            e.ScrimMatchId,
            e.ScrimMatchRound
        });
    }
}
