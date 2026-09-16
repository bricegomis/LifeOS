namespace LifeOS.Application.Households;

public sealed record HouseholdDto(
    Guid Id,
    string Name,
    IReadOnlyList<HouseholdMemberDto> Members,
    IReadOnlyList<MemberProfileDto> MemberProfiles,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record HouseholdMemberDto(
    Guid Id,
    Guid SupabaseUserId,
    string Role,
    DateTimeOffset CreatedAt);

public sealed record MemberProfileDto(
    Guid Id,
    string DisplayName,
    decimal PortionCoefficient,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
