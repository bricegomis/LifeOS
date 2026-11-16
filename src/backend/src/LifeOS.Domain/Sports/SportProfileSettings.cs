namespace LifeOS.Domain.Sports;

/// <summary>Sport settings of one profile (ADR 0015): the effort and discomfort offered when a pass ends.</summary>
public sealed class SportProfileSettings
{
    public const int DefaultEffortValue = 7;
    public const int DefaultDiscomfortValue = 2;

    public Guid HouseholdId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public int DefaultEffort { get; private set; } = DefaultEffortValue;
    public int DefaultDiscomfort { get; private set; } = DefaultDiscomfortValue;

    private SportProfileSettings() { }

    public static SportProfileSettings Create(Guid householdId, Guid memberProfileId)
    {
        if (householdId == Guid.Empty || memberProfileId == Guid.Empty) throw new ArgumentException("Foyer et profil obligatoires.");
        return new SportProfileSettings { HouseholdId = householdId, MemberProfileId = memberProfileId };
    }

    public void Change(int defaultEffort, int defaultDiscomfort)
    {
        if (defaultEffort is < 1 or > 10) throw new ArgumentException("Effort proposé : 1 à 10.");
        if (defaultDiscomfort is < 0 or > 10) throw new ArgumentException("Gêne proposée : 0 à 10.");
        DefaultEffort = defaultEffort;
        DefaultDiscomfort = defaultDiscomfort;
    }
}
