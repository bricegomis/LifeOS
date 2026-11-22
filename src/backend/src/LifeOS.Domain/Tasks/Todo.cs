using LifeOS.Domain.Common;

namespace LifeOS.Domain.Tasks;

/// <summary>
/// Something to do once (ADR 0017): a title, an optional image (else the card shows the title's
/// initial), notes, an optional due day, an estimated duration, a priority, free household tags
/// and a checklist. It belongs to a household profile. An open todo whose due day has passed is
/// late until it is done, postponed or abandoned: nothing slides by itself.
/// </summary>
public sealed class Todo : Entity
{
    public static readonly IReadOnlyDictionary<string, string> Priorities = new Dictionary<string, string>
    {
        ["high"] = "Haute",
        ["normal"] = "Normale",
        ["low"] = "Basse",
    };

    public static readonly IReadOnlyDictionary<string, string> States = new Dictionary<string, string>
    {
        ["open"] = "Ouvert",
        ["done"] = "Fait",
        ["abandoned"] = "Abandonné",
    };

    public const int MaxTitleLength = 200;
    public const int MaxNotesLength = 4000;
    public const int MaxDurationMinutes = 24 * 60;
    public const int MaxTags = 20;
    public const int MaxTagLength = 50;
    public const int MaxChecklistItems = 50;

    private readonly List<ChecklistItem> _checklist = [];

    public Guid HouseholdId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public string Title { get; private set; } = "";
    public string? Notes { get; private set; }
    public DateOnly? DueOn { get; private set; }
    public int? DurationMinutes { get; private set; }
    public string Priority { get; private set; } = "normal";
    public List<string> Tags { get; private set; } = [];
    /// <summary>The project it belongs to, in the same household; none for a loose todo.</summary>
    public Guid? ProjectId { get; private set; }
    public string State { get; private set; } = "open";
    /// <summary>The day it was done; set only in the <c>done</c> state.</summary>
    public DateOnly? DoneOn { get; private set; }
    public string? ImagePath { get; private set; }
    public string? ImageContentType { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyList<ChecklistItem> Checklist => _checklist;
    public IEnumerable<ChecklistItem> OrderedChecklist => _checklist.OrderBy(i => i.Position);
    public bool HasImage => ImagePath is not null;
    public bool IsOpen => State == "open";

    private Todo() { }

    public static Todo Create(Guid householdId, Guid memberProfileId, string title, string? notes, DateOnly? dueOn,
        int? durationMinutes, string? priority, IEnumerable<string>? tags, DateTimeOffset now)
    {
        if (householdId == Guid.Empty) throw new ArgumentException("Foyer obligatoire.");
        if (memberProfileId == Guid.Empty) throw new ArgumentException("Profil obligatoire.");
        var todo = new Todo { Id = Guid.NewGuid(), HouseholdId = householdId, MemberProfileId = memberProfileId, CreatedAt = now };
        todo.Update(title, notes, dueOn, durationMinutes, priority, tags, now);
        return todo;
    }

    public void Update(string title, string? notes, DateOnly? dueOn, int? durationMinutes, string? priority,
        IEnumerable<string>? tags, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > MaxTitleLength)
            throw new ArgumentException($"Titre obligatoire ({MaxTitleLength} caractères maximum).");
        var text = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (text?.Length > MaxNotesLength) throw new ArgumentException($"Notes : {MaxNotesLength} caractères maximum.");
        if (durationMinutes is < 1 or > MaxDurationMinutes)
            throw new ArgumentException($"Durée estimée : de 1 à {MaxDurationMinutes} minutes.");
        var code = string.IsNullOrWhiteSpace(priority) ? "normal" : priority.Trim();
        if (!Priorities.ContainsKey(code)) throw new ArgumentException("Priorité : high, normal ou low.");
        Title = title.Trim();
        Notes = text;
        DueOn = dueOn;
        DurationMinutes = durationMinutes;
        Priority = code;
        Tags = CleanTags(tags);
        UpdatedAt = now;
    }

    /// <summary>Moves the todo into a project of its household (checked by the caller), or out of any.</summary>
    public void MoveToProject(Guid? projectId, DateTimeOffset now)
    {
        ProjectId = projectId == Guid.Empty ? null : projectId;
        UpdatedAt = now;
    }

    /// <summary>Free household tags: trimmed, without duplicates (case ignored), 20 at most.</summary>
    public static List<string> CleanTags(IEnumerable<string>? tags)
    {
        var clean = (tags ?? []).Select(t => t?.Trim() ?? "").Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (clean.Count > MaxTags) throw new ArgumentException($"Une tâche porte au plus {MaxTags} étiquettes.");
        if (clean.Any(t => t.Length > MaxTagLength))
            throw new ArgumentException($"Une étiquette fait au plus {MaxTagLength} caractères.");
        return clean;
    }

    /// <summary>A late todo stays visible until it is done, postponed or abandoned.</summary>
    public bool IsLate(DateOnly today) => IsOpen && DueOn is { } due && due < today;

    public void Complete(DateOnly day, DateOnly today, DateTimeOffset now)
    {
        if (!IsOpen) throw new ArgumentException("Seul un todo ouvert peut être fait.");
        if (day > today) throw new ArgumentException("Un todo ne peut pas être fait dans le futur.");
        State = "done";
        DoneOn = day;
        UpdatedAt = now;
    }

    public void Abandon(DateTimeOffset now)
    {
        if (!IsOpen) throw new ArgumentException("Seul un todo ouvert peut être abandonné.");
        State = "abandoned";
        DoneOn = null;
        UpdatedAt = now;
    }

    /// <summary>A done or abandoned todo can be opened again; its due day is kept.</summary>
    public void Reopen(DateTimeOffset now)
    {
        if (IsOpen) throw new ArgumentException("Ce todo est déjà ouvert.");
        State = "open";
        DoneOn = null;
        UpdatedAt = now;
    }

    public ChecklistItem AddChecklistItem(string label, DateTimeOffset now)
    {
        if (_checklist.Count >= MaxChecklistItems)
            throw new ArgumentException($"Une checklist compte au plus {MaxChecklistItems} sous-tâches.");
        var item = new ChecklistItem(Guid.NewGuid(), _checklist.Count == 0 ? 0 : _checklist.Max(i => i.Position) + 1, label);
        _checklist.Add(item);
        UpdatedAt = now;
        return item;
    }

    /// <summary>Renames or ticks a sub-task. Ticking every sub-task does not complete the todo.</summary>
    public void UpdateChecklistItem(Guid itemId, string label, bool done, DateTimeOffset now)
    {
        var item = _checklist.SingleOrDefault(i => i.Id == itemId) ?? throw new ArgumentException("Sous-tâche introuvable.");
        item.Update(label, done);
        UpdatedAt = now;
    }

    public void RemoveChecklistItem(Guid itemId, DateTimeOffset now)
    {
        var item = _checklist.SingleOrDefault(i => i.Id == itemId) ?? throw new ArgumentException("Sous-tâche introuvable.");
        _checklist.Remove(item);
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
}

/// <summary>One sub-task of a todo's checklist, in order.</summary>
public sealed class ChecklistItem
{
    public const int MaxLabelLength = 200;

    public Guid Id { get; private set; }
    public int Position { get; private set; }
    public string Label { get; private set; } = "";
    public bool Done { get; private set; }

    private ChecklistItem() { }

    internal ChecklistItem(Guid id, int position, string label)
    {
        Id = id;
        Position = position;
        Update(label, false);
    }

    internal void Update(string label, bool done)
    {
        if (string.IsNullOrWhiteSpace(label) || label.Trim().Length > MaxLabelLength)
            throw new ArgumentException($"Sous-tâche obligatoire ({MaxLabelLength} caractères maximum).");
        Label = label.Trim();
        Done = done;
    }
}
