using LifeOS.Domain.Common;

namespace LifeOS.Domain.Food.Foods;

/// <summary>
/// A generic culinary reference (ADR 0010): manual nutrition and digestion, no brand, store or
/// package. It is "À compléter" while one of its eight nutrition fields is missing.
/// </summary>
public sealed class FoodItem : Entity
{
    public const int MaxNameLength = 200;

    public Guid HouseholdId { get; private set; }
    public string Name { get; private set; } = "";
    /// <summary>"100 g", "100 ml", "1 pièce", or empty while unknown.</summary>
    public string ReferenceUnit { get; private set; } = "";
    public Nutrition? Nutrition { get; private set; }
    public string? Fodmap { get; private set; }
    public int? Nova { get; private set; }
    public string? DigestiveNotes { get; private set; }
    public string? ImagePath { get; private set; }
    public string? ImageContentType { get; private set; }
    public bool HasImage => ImagePath is not null;
    public bool IsArchived { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<string> MissingFields =>
        new (string Label, bool Missing)[]
        {
            ("Base nutritionnelle", ReferenceUnit.Length == 0),
            ("Calories", Nutrition?.CaloriesPerUnit is null),
            ("Protéines", Nutrition?.ProteinsPerUnit is null),
            ("Glucides", Nutrition?.CarbsPerUnit is null),
            ("Lipides", Nutrition?.FatsPerUnit is null),
            ("Fibres", Nutrition?.FibersPerUnit is null),
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
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength)
            throw new ArgumentException($"Nom d'aliment requis ({MaxNameLength} caractères maximum).");
        var unit = referenceUnit?.Trim() ?? "";
        if (unit.Length > 0 && !Measures.NutritionBases.Contains(unit))
            throw new ArgumentException("Base nutritionnelle : 100 g, 100 ml ou 1 pièce.");
        Name = name.Trim();
        ReferenceUnit = unit;
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

    public void SetImage(string imagePath, string imageContentType)
    {
        Images.Validate(imagePath, imageContentType);
        ImagePath = imagePath;
        ImageContentType = imageContentType;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RemoveImage()
    {
        ImagePath = null;
        ImageContentType = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Archive() { IsArchived = true; UpdatedAt = DateTimeOffset.UtcNow; }
}
