using LifeOS.Domain.Common;

namespace LifeOS.Domain.Food.Foods;

/// <summary>
/// A generic culinary reference (ADR 0010): manual nutrition and digestion, no brand, store or
/// package. It is "À compléter" while one of its eight nutrition fields is missing. It always has a
/// family; roles, conservation, season months and tags are optional (ADR 0012) and never count in
/// the completion.
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
    /// <summary>Code from <see cref="FoodClassification.Families"/>.</summary>
    public string Family { get; private set; } = "";
    /// <summary>Codes from <see cref="FoodClassification.Roles"/>, in the fixed list order.</summary>
    public List<string> Roles { get; private set; } = [];
    /// <summary>Code from <see cref="FoodClassification.Conservations"/>, or null when not given.</summary>
    public string? Conservation { get; private set; }
    /// <summary>Months 1..12, sorted. Empty means "not seasonal" (available all year).</summary>
    public List<int> SeasonMonths { get; private set; } = [];
    /// <summary>Free household tags, distinct ignoring case.</summary>
    public List<string> Tags { get; private set; } = [];
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

    public static FoodItem CreateManual(Guid householdId, string name, string family, string referenceUnit,
        Nutrition? nutrition = null, DateTimeOffset? now = null)
    {
        if (householdId == Guid.Empty) throw new ArgumentException("Foyer requis.");
        var food = new FoodItem { Id = Guid.NewGuid(), HouseholdId = householdId, CreatedAt = now ?? DateTimeOffset.UtcNow };
        food.UpdateDetails(name, referenceUnit, nutrition, now);
        food.UpdateClassification(family, [], null, [], [], now);
        return food;
    }

    /// <summary>Null when the food is not seasonal, otherwise whether <paramref name="month"/> (1..12) is in season.</summary>
    public bool? IsInSeason(int month) => SeasonMonths.Count == 0 ? null : SeasonMonths.Contains(month);

    public void UpdateClassification(string family, IEnumerable<string>? roles, string? conservation,
        IEnumerable<int>? seasonMonths, IEnumerable<string>? tags, DateTimeOffset? now = null)
    {
        var familyCode = family?.Trim() ?? "";
        if (!FoodClassification.Families.ContainsKey(familyCode))
            throw new ArgumentException($"Famille requise : {FoodClassification.Describe(FoodClassification.Families)}.");

        var roleCodes = (roles ?? []).Select(r => r?.Trim() ?? "").Distinct().ToList();
        if (roleCodes.Any(r => !FoodClassification.Roles.ContainsKey(r)))
            throw new ArgumentException($"Rôle invalide : {FoodClassification.Describe(FoodClassification.Roles)}.");

        var conservationCode = string.IsNullOrWhiteSpace(conservation) ? null : conservation.Trim();
        if (conservationCode is not null && !FoodClassification.Conservations.ContainsKey(conservationCode))
            throw new ArgumentException($"Conservation invalide : {FoodClassification.Describe(FoodClassification.Conservations)}.");

        var months = (seasonMonths ?? []).Distinct().Order().ToList();
        if (months.Any(m => m is < 1 or > 12)) throw new ArgumentException("Les mois de saison vont de 1 à 12.");

        var cleanTags = (tags ?? []).Select(t => t?.Trim() ?? "").Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (cleanTags.Count > FoodClassification.MaxTags)
            throw new ArgumentException($"Un aliment porte au plus {FoodClassification.MaxTags} étiquettes.");
        if (cleanTags.Any(t => t.Length > FoodClassification.MaxTagLength))
            throw new ArgumentException($"Une étiquette fait au plus {FoodClassification.MaxTagLength} caractères.");

        Family = familyCode;
        Roles = FoodClassification.Roles.Keys.Where(roleCodes.Contains).ToList();
        Conservation = conservationCode;
        SeasonMonths = months;
        Tags = cleanTags;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
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
