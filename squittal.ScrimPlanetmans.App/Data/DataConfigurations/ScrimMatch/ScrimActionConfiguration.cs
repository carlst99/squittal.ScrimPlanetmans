using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.Data.DataConfigurations.ScrimMatch;

public class ScrimActionConfiguration : IEntityTypeConfiguration<ScrimAction>
{
    public void Configure(EntityTypeBuilder<ScrimAction> builder)
    {
        builder.ToTable("ScrimAction");

        builder.HasKey(e => e.Action);
    }
}
