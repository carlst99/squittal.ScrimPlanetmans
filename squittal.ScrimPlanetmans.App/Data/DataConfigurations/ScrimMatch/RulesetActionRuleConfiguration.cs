using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.Data.DataConfigurations.ScrimMatch;

public class RulesetActionRuleConfiguration : IEntityTypeConfiguration<RulesetActionRule>
{
    public void Configure(EntityTypeBuilder<RulesetActionRule> builder)
    {
        builder.ToTable("RulesetActionRule");

        builder.HasKey(e => new
        {
            e.RulesetId,
            e.ScrimActionType
        });

        builder.HasOne(rule => rule.Ruleset)
            .WithMany(ruleset => ruleset.RulesetActionRules)
            .HasForeignKey(rule => rule.RulesetId);

        builder.Property(e => e.Points).HasDefaultValue(0);
        builder.Property(e => e.DeferToItemCategoryRules).HasDefaultValue(false);
        builder.Property(e => e.ScrimActionTypeDomain).HasDefaultValue(ScrimActionTypeDomain.Default);
    }
}
