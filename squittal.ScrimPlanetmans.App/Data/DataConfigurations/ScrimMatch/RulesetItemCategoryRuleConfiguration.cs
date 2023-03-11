using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.Data.DataConfigurations.ScrimMatch;

public class RulesetItemCategoryRuleConfiguration : IEntityTypeConfiguration<RulesetItemCategoryRule>
{
    public void Configure(EntityTypeBuilder<RulesetItemCategoryRule> builder)
    {
        builder.ToTable("RulesetItemCategoryRule");

        builder.HasKey(e => new
        {
            e.RulesetId,
            e.ItemCategoryId
        });

        builder.HasOne(rule => rule.Ruleset)
            .WithMany(ruleset => ruleset.RulesetItemCategoryRules)
            .HasForeignKey(rule => rule.RulesetId);

        builder.Property(e => e.Points).HasDefaultValue(0);
        builder.Property(e => e.IsBanned).HasDefaultValue(false);
        builder.Property(e => e.DeferToItemRules).HasDefaultValue(false);
        builder.Property(e => e.DeferToPlanetsideClassSettings).HasDefaultValue(false);
        builder.Property(e => e.InfiltratorIsBanned).HasDefaultValue(false);
        builder.Property(e => e.InfiltratorPoints).HasDefaultValue(false);
        builder.Property(e => e.LightAssaultIsBanned).HasDefaultValue(false);
        builder.Property(e => e.LightAssaultPoints).HasDefaultValue(0);
        builder.Property(e => e.MedicIsBanned).HasDefaultValue(false);
        builder.Property(e => e.MedicPoints).HasDefaultValue(0);
        builder.Property(e => e.EngineerIsBanned).HasDefaultValue(false);
        builder.Property(e => e.EngineerPoints).HasDefaultValue(0);
        builder.Property(e => e.HeavyAssaultIsBanned).HasDefaultValue(false);
        builder.Property(e => e.HeavyAssaultPoints).HasDefaultValue(0);
        builder.Property(e => e.MaxIsBanned).HasDefaultValue(false);
        builder.Property(e => e.MaxPoints).HasDefaultValue(0);
    }
}
