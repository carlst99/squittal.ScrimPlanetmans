using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using squittal.ScrimPlanetmans.App.Data.Models;

namespace squittal.ScrimPlanetmans.App.Data.DataConfigurations.ScrimMatch;

public class ScrimDeathConfiguration : IEntityTypeConfiguration<ScrimDeath>
{
    public void Configure(EntityTypeBuilder<ScrimDeath> builder)
    {
        builder.ToTable("ScrimDeath");

        builder.HasKey(e => new
        {
            e.ScrimMatchId,
            e.Timestamp,
            e.AttackerCharacterId,
            e.VictimCharacterId
        });

        builder.Property(e => e.ScrimMatchRound).HasDefaultValue(-1);

        builder.Ignore(e => e.ScrimMatch);
        builder.Ignore(e => e.AttackerParticipatingPlayer);
        builder.Ignore(e => e.VictimParticipatingPlayer);
        builder.Ignore(e => e.AttackerFaction);
        builder.Ignore(e => e.VictimFaction);
        builder.Ignore(e => e.Weapon);
        builder.Ignore(e => e.WeaponItemCategory);
        builder.Ignore(e => e.AttackerVehicle);
        builder.Ignore(e => e.World);
        builder.Ignore(e => e.Zone);
    }
}
