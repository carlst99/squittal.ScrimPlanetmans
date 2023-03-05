using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using squittal.ScrimPlanetmans.App.Data.Models;

namespace squittal.ScrimPlanetmans.App.Data.DataConfigurations.ScrimMatch;

public class ConstructedTeamFactionPreferenceConfiguration : IEntityTypeConfiguration<ConstructedTeamFactionPreference>
{
    public void Configure(EntityTypeBuilder<ConstructedTeamFactionPreference> builder)
    {
        builder.ToTable("ConstructedTeamFactionPreference");

        builder.HasKey(e => new
        {
            e.ConstructedTeamId,
            e.PreferenceOrdinalValue
        });

        //builder.HasOne(faction => faction.ConstructedTeam)
        //    .WithMany(team => team.FactionPreferences)
        //    .HasForeignKey(faction => faction.ConstructedTeamId);
    }
}
