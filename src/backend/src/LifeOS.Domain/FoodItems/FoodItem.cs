using LifeOS.Domain.Common;

namespace LifeOS.Domain.FoodItems;

public sealed class FoodItem : Entity
{
    public Guid HouseholdId { get; private set; }
    public string Name { get; private set; } = "";
    public string ReferenceUnit { get; private set; } = "";
    public Nutrition? Nutrition { get; private set; }
    public string? Fodmap { get; private set; }
    public int? Nova { get; private set; }
    public string? DigestiveNotes { get; private set; }
    public bool IsArchived { get; private set; }
    public FoodItemSource Source { get; private set; } = FoodItemSource.Manual;
    public string? OffBarcode { get; private set; }
    public string? OffPayload { get; private set; }
    public Guid? IsCorrectionOf { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<string> MissingFields =>
        new (string Label, bool Missing)[]
        {
            ("Base nutritionnelle", string.IsNullOrWhiteSpace(ReferenceUnit)),
            ("Calories", Nutrition?.CaloriesPerUnit is null),
            ("Protéines", Nutrition?.ProteinsPerUnit is null),
            ("Glucides", Nutrition?.CarbsPerUnit is null),
            ("Lipides", Nutrition?.FatsPerUnit is null),
            ("FODMAP", Fodmap is null),
            ("NOVA", Nova is null),
        }.Where(f => f.Missing).Select(f => f.Label).ToList();

    private FoodItem() { }

    public static FoodItem CreateManual(Guid householdId, string name, string referenceUnit,
        Nutrition? nutrition = null, DateTimeOffset? now = null)
    {
        if (householdId == Guid.Empty) throw new ArgumentException("Foyer requis.");
        var food = new FoodItem { Id = Guid.NewGuid(), HouseholdId = householdId, CreatedAt = now ?? DateTimeOffset.UtcNow };
        food.UpdateDetails(name, referenceUnit, nutrition, now);
        return food;
    }

    public void UpdateDetails(string name, string referenceUnit, Nutrition? nutrition, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 500) throw new ArgumentException("Nom d'aliment requis (500 caractères maximum).");
        if (referenceUnit.Length > 50) throw new ArgumentException("Base limitée à 50 caractères.");
        Name = name.Trim();
        ReferenceUnit = referenceUnit.Trim();
        Nutrition = nutrition;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }

    public void UpdateDigestiveInformation(string? fodmap, int? nova, string? notes)
    {
        if (fodmap is not (null or "low" or "moderate" or "high")) throw new ArgumentException("Niveau FODMAP invalide.");
        if (nova is not null && nova is < 1 or > 4) throw new ArgumentException("NOVA doit être compris entre 1 et 4.");
        if (notes?.Length > 2000) throw new ArgumentException("Notes limitées à 2000 caractères.");
        Fodmap = fodmap;
        Nova = nova;
        DigestiveNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Archive() { IsArchived = true; UpdatedAt = DateTimeOffset.UtcNow; }
}
