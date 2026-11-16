using LifeOS.Domain.Common;

namespace LifeOS.Domain.Sports;

/// <summary>
/// A card of the household's exercise library (ADR 0014): a name, an optional image and
/// instructions, what is measured — a hold in seconds or repetitions — and an optional usual load
/// (ADR 0015), offered as the target when the exercise is added to a routine. No load means bodyweight.
/// Routines pick exercises; exercises are archived, not deleted.
/// </summary>
public sealed class Exercise : Entity
{
    public static readonly IReadOnlyDictionary<string, string> Measures = new Dictionary<string, string>
    {
        ["hold"] = "Tenue (secondes)",
        ["reps"] = "Répétitions",
    };

    public const int MaxInstructionsLength = 2000;

    public Guid HouseholdId { get; private set; }
    public string Name { get; private set; } = "";
    public string? Instructions { get; private set; }
    public string Measure { get; private set; } = "";
    /// <summary>Usual load in kg (a weight on the thighs, a dumbbell); null means bodyweight.</summary>
    public decimal? DefaultLoadKg { get; private set; }
    public string? ImagePath { get; private set; }
    public string? ImageContentType { get; private set; }
    public bool IsArchived { get; private set; }
    public bool HasImage => ImagePath is not null;

    private Exercise() { }

    public static Exercise Create(Guid householdId, string name, string? instructions, string measure, decimal? defaultLoadKg)
    {
        if (householdId == Guid.Empty) throw new ArgumentException("Foyer obligatoire.");
        var exercise = new Exercise { Id = Guid.NewGuid(), HouseholdId = householdId };
        exercise.Update(name, instructions, measure, defaultLoadKg, alreadyDone: false);
        return exercise;
    }

    /// <param name="alreadyDone">The exercise has completions: its measure is frozen so its history stays comparable.</param>
    public void Update(string name, string? instructions, string measure, decimal? defaultLoadKg, bool alreadyDone)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            throw new ArgumentException("Nom de l'exercice obligatoire (100 caractères maximum).");
        var code = measure?.Trim() ?? "";
        if (!Measures.ContainsKey(code)) throw new ArgumentException("Mesure : hold (tenue en secondes) ou reps (répétitions).");
        if (alreadyDone && code != Measure)
            throw new ArgumentException("La mesure d'un exercice déjà réalisé ne change pas ; créez un autre exercice.");
        var text = string.IsNullOrWhiteSpace(instructions) ? null : instructions.Trim();
        if (text?.Length > MaxInstructionsLength) throw new ArgumentException("Consigne : 2000 caractères maximum.");
        Name = name.Trim();
        Instructions = text;
        Measure = code;
        DefaultLoadKg = RoutineLine.NormalizeLoad(defaultLoadKg);
    }

    public void SetImage(string imagePath, string imageContentType)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || imagePath.Length > 500) throw new ArgumentException("Référence d'image invalide.");
        if (imageContentType is not ("image/jpeg" or "image/png" or "image/webp")) throw new ArgumentException("Format d'image invalide.");
        ImagePath = imagePath;
        ImageContentType = imageContentType;
    }

    public void RemoveImage()
    {
        ImagePath = null;
        ImageContentType = null;
    }

    public void Archive() => IsArchived = true;
}
