using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.Data.DataConfigurations.ScrimMatch;

public class RulesetFacilityRuleConfiguration : IEntityTypeConfiguration<RulesetFacilityRule>
{
    public void Configure(EntityTypeBuilder<RulesetFacilityRule> builder)
    {
        builder.ToTable("RulesetFacilityRule");

        builder.HasKey(e => new { e.RulesetId, e.FacilityId });

        builder.HasOne(e => e.Ruleset)
            .WithMany(r => r.RulesetFacilityRules)
            .HasForeignKey(e => e.RulesetId);
    }
}
