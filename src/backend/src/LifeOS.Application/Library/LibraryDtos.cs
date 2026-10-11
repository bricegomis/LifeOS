namespace LifeOS.Application.Library;

public sealed record ActivityDto(
    string Id,
    string Name,
    string Icon,
    int? DefaultDurationMinutes);
