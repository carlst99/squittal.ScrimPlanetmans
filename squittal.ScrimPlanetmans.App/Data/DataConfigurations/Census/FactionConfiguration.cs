using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using squittal.ScrimPlanetmans.App.Models.Planetside;

namespace squittal.ScrimPlanetmans.App.Data.DataConfigurations.Census;

public class FactionConfiguration : IEntityTypeConfiguration<Faction>
{
    public void Configure(EntityTypeBuilder<Faction> builder)
    {
        builder.ToTable("Faction");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).ValueGeneratedNever();
    }
}
