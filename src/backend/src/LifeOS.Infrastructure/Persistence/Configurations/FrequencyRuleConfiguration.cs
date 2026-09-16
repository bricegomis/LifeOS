using System.Text.Json;
using LifeOS.Domain.Households;
using LifeOS.Domain.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class FrequencyRuleConfiguration : IEntityTypeConfiguration<FrequencyRule>
{
    public void Configure(EntityTypeBuilder<FrequencyRule> builder)
    {
        builder.ToTable("frequency_rules");

        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.Id).ValueGeneratedNever();

        builder.Property(rule => rule.HouseholdId).IsRequired();

        builder.Property(rule => rule.Target)
            .HasConversion(
                target => FrequencyRuleTargetJson.Serialize(target),
                value => FrequencyRuleTargetJson.Deserialize(value))
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(rule => rule.TargetCountPerWeek).IsRequired();

        builder.HasOne<Household>()
            .WithMany()
            .HasForeignKey(rule => rule.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(rule => rule.HouseholdId);
    }
}

internal static class FrequencyRuleTargetJson
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Serialize(FrequencyRuleTarget target)
    {
        var payload = target switch
        {
            FrequencyRuleTarget.Component component => new FrequencyRuleTargetPayload(
                "component",
                component.ComponentId,
                null,
                null,
                null),
            FrequencyRuleTarget.Dish dish => new FrequencyRuleTargetPayload("dish", null, dish.DishId, null, null),
            FrequencyRuleTarget.Category category => new FrequencyRuleTargetPayload(
                "category",
                null,
                null,
                category.CategoryId,
                category.Label),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown frequency rule target."),
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static FrequencyRuleTarget Deserialize(string value)
    {
        var payload = JsonSerializer.Deserialize<FrequencyRuleTargetPayload>(value, JsonOptions)
            ?? throw new InvalidOperationException("Frequency rule target payload is invalid.");

        return payload.Kind switch
        {
            "component" when payload.ComponentId is not null => new FrequencyRuleTarget.Component(payload.ComponentId),
            "dish" when payload.DishId is not null => new FrequencyRuleTarget.Dish(payload.DishId),
            "category" when payload.CategoryId is not null && payload.Label is not null
                => new FrequencyRuleTarget.Category(payload.CategoryId, payload.Label),
            _ => throw new InvalidOperationException($"Frequency rule target kind '{payload.Kind}' is invalid."),
        };
    }

    private sealed record FrequencyRuleTargetPayload(
        string Kind,
        string? ComponentId,
        string? DishId,
        string? CategoryId,
        string? Label);
}
