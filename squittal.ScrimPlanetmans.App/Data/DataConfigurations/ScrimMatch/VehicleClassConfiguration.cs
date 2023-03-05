using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.Data.DataConfigurations.ScrimMatch;

public class VehicleClassConfiguration : IEntityTypeConfiguration<VehicleClass>
{
    public void Configure(EntityTypeBuilder<VehicleClass> builder)
    {
        builder.ToTable("VehicleClass");

        builder.HasKey(e => e.Class);
    }
}
