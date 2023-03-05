using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.Data.DataConfigurations.ScrimMatch;

public class RulesetConfiguration : IEntityTypeConfiguration<Ruleset>
{
    public void Configure(EntityTypeBuilder<Ruleset> builder)
    {
        builder.ToTable("Ruleset");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.IsCustomDefault).HasDefaultValue(false);
        builder.Property(e => e.IsDefault).HasDefaultValue(false);
        builder.Property(e => e.DefaultRoundLength).HasDefaultValue(900);
        builder.Property(e => e.DefaultMatchTitle).HasDefaultValue(null);
        builder.Property(e => e.SourceFile).HasDefaultValue(string.Empty);
        builder.Property(e => e.DefaultEndRoundOnFacilityCapture).HasDefaultValue(false);
    }
}
