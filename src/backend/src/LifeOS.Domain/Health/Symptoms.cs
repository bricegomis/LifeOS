using LifeOS.Domain.Common;

namespace LifeOS.Domain.Health;

/// <summary>A symptom of the owner's own list (ADR 0018): physical or mental; archived rather than deleted.</summary>
public sealed class SymptomType : Entity
{
    public static readonly IReadOnlyDictionary<string, string> Kinds = new Dictionary<string, string>
    {
        ["physique"] = "Physique", ["mental"] = "Mental",
    };

    /// <summary>The list proposed when the owner has none yet (the needs expressed on 2026-11-22).</summary>
    public static readonly IReadOnlyList<(string Name, string Kind)> Defaults =
    [
        ("Mal de ventre", "physique"), ("Diarrhée", "physique"), ("Constipation", "physique"), ("Mal de dos", "physique"),
        ("Mal au genou", "physique"), ("Mal à la nuque", "physique"), ("Fatigue", "physique"), ("Flou mental", "mental"),
        ("Syndrome dépressif", "mental"), ("Envie de rien", "mental"),
    ];

    public Guid HouseholdId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public string Name { get; private set; } = "";
    public string Kind { get; private set; } = "physique";
    public bool IsArchived { get; private set; }

    private SymptomType() { }

    public static SymptomType Create(Guid householdId, Guid ownerUserId, Guid memberProfileId, string name, string? kind)
    {
        if (householdId == Guid.Empty || ownerUserId == Guid.Empty || memberProfileId == Guid.Empty)
            throw new ArgumentException("Foyer, compte et profil obligatoires.");
        var type = new SymptomType { Id = Guid.NewGuid(), HouseholdId = householdId, OwnerUserId = ownerUserId, MemberProfileId = memberProfileId };
        type.Update(name, kind);
        return type;
    }

    public void Update(string name, string? kind)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100) throw new ArgumentException("Nom du symptôme obligatoire (100 caractères maximum).");
        var code = string.IsNullOrWhiteSpace(kind) ? "physique" : kind.Trim();
        if (!Kinds.ContainsKey(code)) throw new ArgumentException("Type : physique ou mental.");
        Name = name.Trim();
        Kind = code;
    }

    public void Archive() => IsArchived = true;
    public void Restore() => IsArchived = false;
}

/// <summary>A symptom felt on a day (and at an optional time), with an intensity from 1 (light) to 10 (very strong).</summary>
public sealed class SymptomEntry : Entity
{
    public const int MaxNoteLength = 1000;

    public Guid HouseholdId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public Guid SymptomTypeId { get; private set; }
    public DateOnly Day { get; private set; }
    public TimeOnly? Time { get; private set; }
    public int Intensity { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private SymptomEntry() { }

    public static SymptomEntry Create(SymptomType type, DateOnly day, TimeOnly? time, int intensity, string? note, DateOnly today, DateTimeOffset now)
    {
        var entry = new SymptomEntry
        {
            Id = Guid.NewGuid(), HouseholdId = type.HouseholdId, OwnerUserId = type.OwnerUserId, MemberProfileId = type.MemberProfileId, CreatedAt = now,
        };
        entry.Update(type, day, time, intensity, note, today);
        return entry;
    }

    public void Update(SymptomType type, DateOnly day, TimeOnly? time, int intensity, string? note, DateOnly today)
    {
        if (type.OwnerUserId != OwnerUserId || type.HouseholdId != HouseholdId) throw new ArgumentException("Symptôme introuvable.");
        if (day > today) throw new ArgumentException("Un symptôme ne peut pas être noté dans le futur.");
        if (intensity is < 1 or > 10) throw new ArgumentException("Intensité : de 1 (léger) à 10 (très fort).");
        var text = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (text?.Length > MaxNoteLength) throw new ArgumentException($"Note : {MaxNoteLength} caractères maximum.");
        SymptomTypeId = type.Id;
        Day = day;
        Time = time;
        Intensity = intensity;
        Note = text;
    }
}

/// <summary>A day noted as "rien à signaler": it counts as a noted day without any symptom.</summary>
public sealed class CalmDay : Entity
{
    public Guid HouseholdId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public DateOnly Day { get; private set; }

    private CalmDay() { }

    public static CalmDay Create(Guid householdId, Guid ownerUserId, Guid memberProfileId, DateOnly day, DateOnly today)
    {
        if (day > today) throw new ArgumentException("Un jour ne peut pas être noté dans le futur.");
        return new CalmDay { Id = Guid.NewGuid(), HouseholdId = householdId, OwnerUserId = ownerUserId, MemberProfileId = memberProfileId, Day = day };
    }
}
