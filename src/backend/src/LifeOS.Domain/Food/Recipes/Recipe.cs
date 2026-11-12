using LifeOS.Domain.Common;

namespace LifeOS.Domain.Food.Recipes;

/// <summary>
/// A library preparation: ingredients picked among the household's foods, a yield in
/// servings, optional instructions, duration, tags and image. It never references a product or brand.
/// </summary>
public sealed class Recipe : Entity
{
    public const int MaxNameLength = 200;
    public const int MaxInstructionsLength = 10000;
    public const int MaxTags = 20;
    public const int MaxTagLength = 50;
    /// <summary>Tags are stored joined by this character, so a tag cannot contain it.</summary>
    public const char TagSeparator = '|';

    public Guid HouseholdId { get; private set; }
    public bool IsArchived { get; private set; }
    public string Name { get; private set; } = "";
    public int Servings { get; private set; }
    public int DurationMinutes { get; private set; }
    public string? Instructions { get; private set; }
    public List<string> Tags { get; private set; } = [];
    public string? ImagePath { get; private set; }
    public string? ImageContentType { get; private set; }
    public bool HasImage => ImagePath is not null;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public List<RecipeIngredient> Ingredients { get; private set; } = [];

    private Recipe() { }

    public static Recipe Create(Guid householdId, string name, int servings, int durationMinutes,
        IEnumerable<string>? tags = null, string? instructions = null, DateTimeOffset? now = null)
    {
        if (householdId == Guid.Empty) throw new ArgumentException("Une recette appartient à un foyer.");
        var timestamp = now ?? DateTimeOffset.UtcNow;
        var recipe = new Recipe { Id = Guid.NewGuid(), HouseholdId = householdId, CreatedAt = timestamp };
        recipe.Apply(name, servings, durationMinutes, tags ?? [], instructions, timestamp);
        return recipe;
    }

    /// <summary>Partial update: a null argument leaves the field unchanged; a blank instruction clears it.</summary>
    public void Update(string? name = null, int? servings = null, int? durationMinutes = null,
        IEnumerable<string>? tags = null, string? instructions = null, DateTimeOffset? now = null) =>
        Apply(name ?? Name, servings ?? Servings, durationMinutes ?? DurationMinutes, tags ?? Tags,
            instructions ?? Instructions, now ?? DateTimeOffset.UtcNow);

    public RecipeIngredient AddIngredient(Guid foodItemId, decimal quantity, string unit, DateTimeOffset? now = null)
    {
        var ingredient = RecipeIngredient.Create(Id, foodItemId, quantity, unit);
        Ingredients.Add(ingredient);
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
        return ingredient;
    }

    /// <summary>Returns false when the ingredient is not part of this recipe.</summary>
    public bool UpdateIngredient(Guid ingredientId, decimal quantity, string unit, DateTimeOffset? now = null)
    {
        var ingredient = Ingredients.SingleOrDefault(i => i.Id == ingredientId);
        if (ingredient is null) return false;
        ingredient.Update(quantity, unit);
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
        return true;
    }

    /// <summary>Returns false when the ingredient is not part of this recipe.</summary>
    public bool RemoveIngredient(Guid ingredientId, DateTimeOffset? now = null)
    {
        var ingredient = Ingredients.SingleOrDefault(i => i.Id == ingredientId);
        if (ingredient is null) return false;
        Ingredients.Remove(ingredient);
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
        return true;
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

    public void Archive(DateTimeOffset? now = null)
    {
        IsArchived = true;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }

    private void Apply(string name, int servings, int durationMinutes, IEnumerable<string> tags,
        string? instructions, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength)
            throw new ArgumentException($"Nom de recette requis ({MaxNameLength} caractères maximum).");
        if (servings <= 0) throw new ArgumentException("Le rendement doit être d'au moins une portion.");
        if (durationMinutes < 0) throw new ArgumentException("La durée ne peut pas être négative.");
        if (instructions?.Length > MaxInstructionsLength)
            throw new ArgumentException("Les instructions ne peuvent pas dépasser 10 000 caractères.");
        var cleanTags = tags.Select(tag => tag?.Trim() ?? "").Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (cleanTags.Count > MaxTags) throw new ArgumentException($"Une recette porte au plus {MaxTags} catégories.");
        if (cleanTags.Any(tag => tag.Length > MaxTagLength || tag.Contains(TagSeparator)))
            throw new ArgumentException($"Une catégorie fait au plus {MaxTagLength} caractères et ne contient pas « {TagSeparator} ».");

        Name = name.Trim();
        Servings = servings;
        DurationMinutes = durationMinutes;
        Tags = cleanTags;
        Instructions = string.IsNullOrWhiteSpace(instructions) ? null : instructions;
        UpdatedAt = now;
    }
}
