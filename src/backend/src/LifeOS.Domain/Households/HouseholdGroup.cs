using LifeOS.Domain.Common;

namespace LifeOS.Domain.Households;

public sealed class HouseholdGroup : Entity
{
    public Guid HouseholdId { get; private set; }
    public string Name { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private HouseholdGroup() { }

    private HouseholdGroup(Guid id, Guid householdId, string name, DateTimeOffset now) : base(id)
    {
        HouseholdId = householdId;
        Name = name;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public static HouseholdGroup Create(Guid householdId, string name)
    {
        Validate(householdId, name);
        return new(Guid.NewGuid(), householdId, name.Trim(), DateTimeOffset.UtcNow);
    }

    public void Rename(string name)
    {
        Validate(HouseholdId, name);
        Name = name.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void Validate(Guid householdId, string name)
    {
        if (householdId == Guid.Empty) throw new ArgumentException("Le foyer est obligatoire.");
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            throw new ArgumentException("Le nom du groupe doit contenir entre 1 et 100 caractères.");
    }
}
