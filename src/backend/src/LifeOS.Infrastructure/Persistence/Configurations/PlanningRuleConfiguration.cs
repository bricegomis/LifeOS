using System.Text.Json;
using LifeOS.Domain.Households;
using LifeOS.Domain.Library;
using LifeOS.Domain.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class PlanningRuleConfiguration : IEntityTypeConfiguration<PlanningRule>
{
    public void Configure(EntityTypeBuilder<PlanningRule> builder)
    {
        builder.ToTable("planning_rules");

        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.Id).ValueGeneratedNever();

        builder.Property(rule => rule.HouseholdId).IsRequired();
        builder.Property(rule => rule.Weekday).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(rule => rule.MealType).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(rule => rule.Target)
            .HasConversion(
                target => PlanningRuleTargetJson.Serialize(target),
                value => PlanningRuleTargetJson.Deserialize(value))
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasOne<Household>()
            .WithMany()
            .HasForeignKey(rule => rule.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(rule => rule.HouseholdId);
        builder.HasIndex(rule => new { rule.HouseholdId, rule.Weekday, rule.MealType });
    }
}

internal static class PlanningRuleTargetJson
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Serialize(PlanningRuleTarget target)
    {
        var payload = target switch
        {
            PlanningRuleTarget.Component component => new PlanningRuleTargetPayload(
                "component",
                component.ComponentId,
                component.ComponentType.ToString(),
                null),
            PlanningRuleTarget.Dish dish => new PlanningRuleTargetPayload("dish", null, null, dish.DishId),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown planning rule target."),
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static PlanningRuleTarget Deserialize(string value)
    {
        var payload = JsonSerializer.Deserialize<PlanningRuleTargetPayload>(value, JsonOptions)
            ?? throw new InvalidOperationException("Planning rule target payload is invalid.");

        return payload.Kind switch
        {
            "component" when payload.ComponentId is not null && payload.ComponentType is not null
                => new PlanningRuleTarget.Component(payload.ComponentId, ParseComponentType(payload.ComponentType)),
            "dish" when payload.DishId is not null => new PlanningRuleTarget.Dish(payload.DishId),
            _ => throw new InvalidOperationException($"Planning rule target kind '{payload.Kind}' is invalid."),
        };
    }

    private static ComponentType ParseComponentType(string componentType) =>
        Enum.TryParse<ComponentType>(componentType, ignoreCase: true, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Planning rule component type '{componentType}' is invalid.");

    private sealed record PlanningRuleTargetPayload(string Kind, string? ComponentId, string? ComponentType, string? DishId);
}
