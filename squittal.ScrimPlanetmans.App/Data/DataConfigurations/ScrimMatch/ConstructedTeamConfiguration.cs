using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using squittal.ScrimPlanetmans.App.Data.Models;

namespace squittal.ScrimPlanetmans.App.Data.DataConfigurations.ScrimMatch;

public class ConstructedTeamConfiguration : IEntityTypeConfiguration<ConstructedTeam>
{
    public void Configure(EntityTypeBuilder<ConstructedTeam> builder)
    {
        builder.ToTable("ConstructedTeam");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.IsHiddenFromSelection).HasDefaultValue(false);
    }
}
