using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace squittal.ScrimPlanetmans.App.Data.DataConfigurations.ScrimMatch;

public class ScrimMatchConfiguration : IEntityTypeConfiguration<Models.ScrimMatch>
{
    public void Configure(EntityTypeBuilder<Models.ScrimMatch> builder)
    {
        builder.ToTable("ScrimMatch");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Ignore(e => e.Ruleset);

        builder.HasOne(e => e.Ruleset)
            .WithMany();

        builder.Property(e => e.RulesetId).HasDefaultValue(-1);

    }
}
