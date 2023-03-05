using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using squittal.ScrimPlanetmans.App.Models.Planetside;

namespace squittal.ScrimPlanetmans.App.Data.DataConfigurations.Census;

public class FacilityTypeConfiguration : IEntityTypeConfiguration<FacilityType>
{
    public void Configure(EntityTypeBuilder<FacilityType> builder)
    {
        builder.ToTable("FacilityType");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).ValueGeneratedNever();
    }
}
