using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using squittal.ScrimPlanetmans.App.Models.ScrimMatchReports;

namespace squittal.ScrimPlanetmans.App.Data.DataConfigurations.ScrimMatchReports;

public class ScrimMatchInfoConfiguration : IEntityTypeConfiguration<ScrimMatchInfo>
{
    public void Configure(EntityTypeBuilder<ScrimMatchInfo> builder)
    {
        builder.ToView("View_ScrimMatchInfo");

        builder.HasNoKey();

        builder.Ignore(e => e.TeamAliases);
    }
}
