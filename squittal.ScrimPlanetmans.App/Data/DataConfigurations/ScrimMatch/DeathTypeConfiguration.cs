using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.Data.DataConfigurations.ScrimMatch;

public class DeathTypeConfiguration : IEntityTypeConfiguration<DeathType>
{
    public void Configure(EntityTypeBuilder<DeathType> builder)
    {
        builder.ToTable("DeathType");

        builder.HasKey(e => e.Type);
    }
}
