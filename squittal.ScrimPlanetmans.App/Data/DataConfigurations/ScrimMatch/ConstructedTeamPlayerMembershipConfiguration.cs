using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using squittal.ScrimPlanetmans.App.Data.Models;

namespace squittal.ScrimPlanetmans.App.Data.DataConfigurations.ScrimMatch;

public class ConstructedTeamPlayerMembershipConfiguration : IEntityTypeConfiguration<ConstructedTeamPlayerMembership>
{
    public void Configure(EntityTypeBuilder<ConstructedTeamPlayerMembership> builder)
    {
        builder.ToTable("ConstructedTeamPlayerMembership");

        builder.HasKey(e => new
        {
            e.ConstructedTeamId,
            e.CharacterId
        });

        builder.HasOne(member => member.ConstructedTeam)
            .WithMany(team => team.PlayerMemberships)
            .HasForeignKey(member => member.ConstructedTeamId);
    }
}
