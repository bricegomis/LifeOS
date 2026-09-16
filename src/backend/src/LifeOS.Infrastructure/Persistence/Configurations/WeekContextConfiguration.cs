using System.Text.Json;
using LifeOS.Domain.Common;
using LifeOS.Domain.Households;
using LifeOS.Domain.WeekContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class WeekContextConfiguration : IEntityTypeConfiguration<WeekContext>
{
    public void Configure(EntityTypeBuilder<WeekContext> builder)
    {
        builder.ToTable("week_contexts");

        builder.HasKey(context => context.HouseholdId);
        builder.Property(context => context.HouseholdId).ValueGeneratedNever();

        builder.Property(context => context.AlternatingWeekConfig)
            .HasConversion(
                value => WeekContextJson.SerializeAlternatingWeekConfig(value),
                value => WeekContextJson.DeserializeAlternatingWeekConfig(value))
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Ignore(context => context.WeekModeOverrides);
        builder.Ignore(context => context.Days);

        var weekModeOverridesProperty = builder.Property<List<WeekModeOverride>>("_weekModeOverrides")
            .HasColumnName("week_mode_overrides")
            .HasConversion(
                value => WeekContextJson.SerializeWeekModeOverrides(value),
                value => WeekContextJson.DeserializeWeekModeOverrides(value))
            .HasColumnType("jsonb");
        weekModeOverridesProperty.Metadata.SetValueComparer(WeekContextJson.WeekModeOverridesComparer);

        var daysProperty = builder.Property<Dictionary<Weekday, DayContext>>("_days")
            .HasColumnName("days")
            .HasConversion(
                value => WeekContextJson.SerializeDays(value),
                value => WeekContextJson.DeserializeDays(value))
            .HasColumnType("jsonb");
        daysProperty.Metadata.SetValueComparer(WeekContextJson.DaysComparer);

        builder.HasOne<Household>()
            .WithOne()
            .HasForeignKey<WeekContext>(context => context.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal static class WeekContextJson
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static readonly ValueComparer<List<WeekModeOverride>> WeekModeOverridesComparer = new(
        (left, right) => WeekModeOverridesEqual(left, right),
        value => GetWeekModeOverridesHash(value),
        value => CloneWeekModeOverrides(value));

    public static readonly ValueComparer<Dictionary<Weekday, DayContext>> DaysComparer = new(
        (left, right) => DaysEqual(left, right),
        value => GetDaysHash(value),
        value => CloneDays(value));

    public static string SerializeAlternatingWeekConfig(AlternatingWeekConfig value)
    {
        var payload = new AlternatingWeekConfigPayload(
            value.ReferenceWeekStartDate,
            value.ReferenceWeekMode.ToString());

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static AlternatingWeekConfig DeserializeAlternatingWeekConfig(string value)
    {
        var payload = JsonSerializer.Deserialize<AlternatingWeekConfigPayload>(value, JsonOptions)
            ?? throw new InvalidOperationException("Week context alternating week configuration is invalid.");

        return new AlternatingWeekConfig(payload.ReferenceWeekStartDate, ParseWeekMode(payload.ReferenceWeekMode));
    }

    public static string SerializeWeekModeOverrides(IEnumerable<WeekModeOverride> overrides)
    {
        var payload = overrides
            .Select(weekModeOverride => new WeekModeOverridePayload(
                weekModeOverride.WeekStartDate,
                weekModeOverride.Mode.ToString()))
            .ToList();

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static List<WeekModeOverride> DeserializeWeekModeOverrides(string value)
    {
        var payload = JsonSerializer.Deserialize<List<WeekModeOverridePayload>>(value, JsonOptions)
            ?? throw new InvalidOperationException("Week context week mode overrides are invalid.");

        return payload
            .Select(weekModeOverride => new WeekModeOverride(
                weekModeOverride.WeekStartDate,
                ParseWeekMode(weekModeOverride.Mode)))
            .ToList();
    }

    public static string SerializeDays(IReadOnlyDictionary<Weekday, DayContext> days)
    {
        var payload = days.ToDictionary(
            pair => pair.Key.ToString(),
            pair => new DayContextPayload(pair.Value.WorkLocation.ToString(), pair.Value.BikeCommute));

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static Dictionary<Weekday, DayContext> DeserializeDays(string value)
    {
        var payload = JsonSerializer.Deserialize<Dictionary<string, DayContextPayload>>(value, JsonOptions)
            ?? throw new InvalidOperationException("Week context days payload is invalid.");

        return payload.ToDictionary(
            pair => ParseWeekday(pair.Key),
            pair => new DayContext(ParseWorkLocation(pair.Value.WorkLocation), pair.Value.BikeCommute));
    }

    private static bool WeekModeOverridesEqual(List<WeekModeOverride>? left, List<WeekModeOverride>? right) =>
        SerializeWeekModeOverrides(left ?? Enumerable.Empty<WeekModeOverride>()) ==
        SerializeWeekModeOverrides(right ?? Enumerable.Empty<WeekModeOverride>());

    private static int GetWeekModeOverridesHash(List<WeekModeOverride>? value) =>
        SerializeWeekModeOverrides(value ?? Enumerable.Empty<WeekModeOverride>()).GetHashCode();

    private static List<WeekModeOverride> CloneWeekModeOverrides(List<WeekModeOverride>? value) =>
        DeserializeWeekModeOverrides(SerializeWeekModeOverrides(value ?? Enumerable.Empty<WeekModeOverride>()));

    private static bool DaysEqual(Dictionary<Weekday, DayContext>? left, Dictionary<Weekday, DayContext>? right) =>
        SerializeDays(left ?? new Dictionary<Weekday, DayContext>()) ==
        SerializeDays(right ?? new Dictionary<Weekday, DayContext>());

    private static int GetDaysHash(Dictionary<Weekday, DayContext>? value) =>
        SerializeDays(value ?? new Dictionary<Weekday, DayContext>()).GetHashCode();

    private static Dictionary<Weekday, DayContext> CloneDays(Dictionary<Weekday, DayContext>? value) =>
        DeserializeDays(SerializeDays(value ?? new Dictionary<Weekday, DayContext>()));

    private static Weekday ParseWeekday(string weekday) =>
        Enum.TryParse<Weekday>(weekday, ignoreCase: true, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Week context weekday '{weekday}' is invalid.");

    private static WeekMode ParseWeekMode(string mode) =>
        Enum.TryParse<WeekMode>(mode, ignoreCase: true, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Week context mode '{mode}' is invalid.");

    private static WorkLocation ParseWorkLocation(string workLocation) =>
        Enum.TryParse<WorkLocation>(workLocation, ignoreCase: true, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Week context work location '{workLocation}' is invalid.");

    private sealed record AlternatingWeekConfigPayload(DateOnly ReferenceWeekStartDate, string ReferenceWeekMode);

    private sealed record WeekModeOverridePayload(DateOnly WeekStartDate, string Mode);

    private sealed record DayContextPayload(string WorkLocation, bool BikeCommute);
}
