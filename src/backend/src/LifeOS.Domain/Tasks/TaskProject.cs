using LifeOS.Domain.Common;

namespace LifeOS.Domain.Tasks;

/// <summary>
/// A plain group of todos (ADR 0017): a name, an optional image (else its initial), notes, an optional
/// due day, an optional parent project of the same household (a tree of any depth, never a cycle) and a
/// state. No steps, dependencies or milestones; its progress is computed from its todos and those of its
/// sub-projects. Finishing or abandoning it touches neither its open todos nor its sub-projects.
/// </summary>
public sealed class TaskProject : Entity
{
    public static readonly IReadOnlyDictionary<string, string> States = new Dictionary<string, string>
    {
        ["active"] = "En cours",
        ["done"] = "Terminé",
        ["abandoned"] = "Abandonné",
    };

    public Guid HouseholdId { get; private set; }
    public Guid? ParentId { get; private set; }
    public string Title { get; private set; } = "";
    public string? Notes { get; private set; }
    public DateOnly? DueOn { get; private set; }
    public string State { get; private set; } = "active";
    public string? ImagePath { get; private set; }
    public string? ImageContentType { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public bool HasImage => ImagePath is not null;
    public bool IsActive => State == "active";

    private TaskProject() { }

    public static TaskProject Create(Guid householdId, string title, string? notes, DateOnly? dueOn, DateTimeOffset now)
    {
        if (householdId == Guid.Empty) throw new ArgumentException("Foyer obligatoire.");
        var project = new TaskProject { Id = Guid.NewGuid(), HouseholdId = householdId, CreatedAt = now };
        project.Update(title, notes, dueOn, now);
        return project;
    }

    public void Update(string title, string? notes, DateOnly? dueOn, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > Todo.MaxTitleLength)
            throw new ArgumentException($"Nom du projet obligatoire ({Todo.MaxTitleLength} caractères maximum).");
        var text = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (text?.Length > Todo.MaxNotesLength) throw new ArgumentException($"Notes : {Todo.MaxNotesLength} caractères maximum.");
        Title = title.Trim();
        Notes = text;
        DueOn = dueOn;
        UpdatedAt = now;
    }

    /// <summary>
    /// Moves the project under <paramref name="parentId"/> (a project of its household, checked by the
    /// caller), or to the top when null. <paramref name="parentOf"/> gives each household project's
    /// parent: the new parent can be neither the project itself nor one of its sub-projects.
    /// </summary>
    public void MoveUnder(Guid? parentId, Func<Guid, Guid?> parentOf, DateTimeOffset now)
    {
        var id = parentId == Guid.Empty ? null : parentId;
        for (var cursor = id; cursor is { } current; cursor = parentOf(current))
            if (current == Id) throw new ArgumentException("Un projet ne peut pas être rangé dans lui-même ni dans un de ses sous-projets.");
        ParentId = id;
        UpdatedAt = now;
    }

    /// <summary>Finished; its open todos stay open and keep the project.</summary>
    public void Finish(DateTimeOffset now) => Close("done", now);

    public void Abandon(DateTimeOffset now) => Close("abandoned", now);

    public void Reopen(DateTimeOffset now)
    {
        if (IsActive) throw new ArgumentException("Ce projet est déjà en cours.");
        State = "active";
        UpdatedAt = now;
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

    private void Close(string state, DateTimeOffset now)
    {
        if (!IsActive) throw new ArgumentException("Seul un projet en cours peut être terminé ou abandonné.");
        State = state;
        UpdatedAt = now;
    }
}
