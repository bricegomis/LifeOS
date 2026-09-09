namespace LifeOS.Domain.Library;

/// <summary>
/// A physical activity that can be scheduled on a day plan, mirroring the frontend's
/// <c>Activity</c> model. Part of the shared, read-only meal library.
/// </summary>
public sealed record Activity(
    string Id,
    string Name,
    string Icon,
    int? DefaultDurationMinutes);
