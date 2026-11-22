using LifeOS.Domain.Common;

namespace LifeOS.Domain.Health;

/// <summary>
/// A blood (or other) marker of the owner's library (ADR 0018): a unit, the laboratory's reference
/// range, a personal target (often narrower, to be in very good shape), how often to measure it again
/// (0 = once) and the panels it belongs to (free names, e.g. "Bilan fer").
/// </summary>
public sealed class Marker : Entity
{
    public const int MaxPanels = 10;

    public Guid HouseholdId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public string Name { get; private set; } = "";
    public string Unit { get; private set; } = "";
    public decimal? LabMin { get; private set; }
    public decimal? LabMax { get; private set; }
    public decimal? TargetMin { get; private set; }
    public decimal? TargetMax { get; private set; }
    /// <summary>Months between two measures; 0 = once.</summary>
    public int FrequencyMonths { get; private set; }
    public List<string> Panels { get; private set; } = [];
    public bool IsArchived { get; private set; }

    private Marker() { }

    public static Marker Create(Guid householdId, Guid ownerUserId, Guid memberProfileId, string name, string unit, decimal? labMin, decimal? labMax,
        decimal? targetMin, decimal? targetMax, int frequencyMonths, IEnumerable<string>? panels)
    {
        if (householdId == Guid.Empty || ownerUserId == Guid.Empty || memberProfileId == Guid.Empty)
            throw new ArgumentException("Foyer, compte et profil obligatoires.");
        var marker = new Marker { Id = Guid.NewGuid(), HouseholdId = householdId, OwnerUserId = ownerUserId, MemberProfileId = memberProfileId };
        marker.Update(name, unit, labMin, labMax, targetMin, targetMax, frequencyMonths, panels);
        return marker;
    }

    public void Update(string name, string unit, decimal? labMin, decimal? labMax, decimal? targetMin, decimal? targetMax, int frequencyMonths,
        IEnumerable<string>? panels)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100) throw new ArgumentException("Nom du marqueur obligatoire (100 caractères maximum).");
        if (string.IsNullOrWhiteSpace(unit) || unit.Trim().Length > 30) throw new ArgumentException("Unité obligatoire (30 caractères maximum).");
        if (labMin > labMax) throw new ArgumentException("Plage du labo : le minimum dépasse le maximum.");
        if (targetMin > targetMax) throw new ArgumentException("Cible : le minimum dépasse le maximum.");
        if (frequencyMonths is < 0 or > 120) throw new ArgumentException("Fréquence : de 0 (une fois) à 120 mois.");
        var cleanPanels = (panels ?? []).Select(p => p?.Trim() ?? "").Where(p => p.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (cleanPanels.Count > MaxPanels || cleanPanels.Any(p => p.Length > 60)) throw new ArgumentException("Bilans : 10 au plus, 60 caractères chacun.");
        Name = name.Trim();
        Unit = unit.Trim();
        LabMin = labMin;
        LabMax = labMax;
        TargetMin = targetMin;
        TargetMax = targetMax;
        FrequencyMonths = frequencyMonths;
        Panels = cleanPanels;
    }

    public void Archive() => IsArchived = true;
    public void Restore() => IsArchived = false;

    /// <summary>
    /// Where a value stands: <c>below_lab</c> or <c>above_lab</c> out of the laboratory range, else
    /// <c>in_target</c> or <c>normal</c> (in the lab range but outside the personal target).
    /// </summary>
    public string StatusOf(decimal value)
    {
        if (LabMin is { } labMin && value < labMin) return "below_lab";
        if (LabMax is { } labMax && value > labMax) return "above_lab";
        return DistanceToTarget(value) == 0 ? "in_target" : "normal";
    }

    /// <summary>How far a value is from the personal target (or the lab range without a target); 0 inside.</summary>
    public decimal DistanceToTarget(decimal value)
    {
        var low = TargetMin ?? (TargetMax is null ? LabMin : null);
        var high = TargetMax ?? (TargetMin is null ? LabMax : null);
        if (low is { } l && value < l) return l - value;
        if (high is { } h && value > h) return value - h;
        return 0;
    }

    /// <summary>
    /// From the previous value to the last: <c>better</c> or <c>worse</c> when it moves toward or away from
    /// the target by more than 2 % of its span, else <c>stable</c> (<c>in_target</c> when both are inside).
    /// </summary>
    public string? TrendOf(decimal? previous, decimal last)
    {
        if (previous is not { } before) return null;
        var (a, b) = (DistanceToTarget(before), DistanceToTarget(last));
        if (a == 0 && b == 0) return "in_target";
        var low = TargetMin ?? LabMin ?? 0;
        var high = TargetMax ?? LabMax ?? Math.Max(low, last);
        var span = Math.Max(high - low, Math.Abs(last) * 0.1m);
        if (span == 0) span = 1;
        if (b < a - span * 0.02m) return "better";
        if (b > a + span * 0.02m) return "worse";
        return "stable";
    }

    /// <summary>The day it should be measured again; null when measured once and that is enough.</summary>
    public DateOnly? NextDueOn(DateOnly? lastDay, DateOnly today) => lastDay is not { } last
        ? today
        : FrequencyMonths == 0 ? null : last.AddMonths(FrequencyMonths);
}

/// <summary>A laboratory analysis (ADR 0018): a day, a laboratory, an optional document, and one value per marker measured.</summary>
public sealed class Analysis : Entity
{
    public const int MaxNotesLength = 2000;

    private readonly List<AnalysisValue> _values = [];

    public Guid HouseholdId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public DateOnly Day { get; private set; }
    public string? Laboratory { get; private set; }
    public string? Notes { get; private set; }
    /// <summary>The scanned report it was read from (a health document of the same account).</summary>
    public Guid? DocumentId { get; private set; }
    public IReadOnlyList<AnalysisValue> Values => _values;

    private Analysis() { }

    public static Analysis Create(Guid householdId, Guid ownerUserId, Guid memberProfileId, DateOnly day, string? laboratory, string? notes,
        IEnumerable<(Marker Marker, decimal Value)> values, DateOnly today)
    {
        if (householdId == Guid.Empty || ownerUserId == Guid.Empty || memberProfileId == Guid.Empty)
            throw new ArgumentException("Foyer, compte et profil obligatoires.");
        var analysis = new Analysis { Id = Guid.NewGuid(), HouseholdId = householdId, OwnerUserId = ownerUserId, MemberProfileId = memberProfileId };
        analysis.Update(day, laboratory, notes, values, today);
        return analysis;
    }

    public void Update(DateOnly day, string? laboratory, string? notes, IEnumerable<(Marker Marker, decimal Value)> values, DateOnly today)
    {
        if (day > today) throw new ArgumentException("Une analyse ne peut pas être dans le futur.");
        var lab = string.IsNullOrWhiteSpace(laboratory) ? null : laboratory.Trim();
        if (lab?.Length > 100) throw new ArgumentException("Laboratoire : 100 caractères maximum.");
        var text = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (text?.Length > MaxNotesLength) throw new ArgumentException($"Notes : {MaxNotesLength} caractères maximum.");
        var list = values.ToList();
        if (list.Count == 0) throw new ArgumentException("Saisissez au moins une valeur.");
        if (list.Any(v => v.Marker.OwnerUserId != OwnerUserId || v.Marker.HouseholdId != HouseholdId)) throw new ArgumentException("Marqueur introuvable.");
        if (list.Select(v => v.Marker.Id).Distinct().Count() != list.Count) throw new ArgumentException("Une seule valeur par marqueur.");
        if (list.Any(v => v.Value is < -1_000_000 or > 1_000_000)) throw new ArgumentException("Valeur hors limites.");
        Day = day;
        Laboratory = lab;
        Notes = text;
        _values.Clear();
        _values.AddRange(list.Select(v => new AnalysisValue(v.Marker.Id, v.Value)));
    }

    public void LinkDocument(Guid? documentId) => DocumentId = documentId;
}

public sealed class AnalysisValue
{
    public Guid MarkerId { get; private set; }
    public decimal Value { get; private set; }

    private AnalysisValue() { }

    internal AnalysisValue(Guid markerId, decimal value)
    {
        MarkerId = markerId;
        Value = value;
    }
}

/// <summary>A kind of body measure (weight, waist, resting heart rate…) with its unit.</summary>
public sealed class MeasureType : Entity
{
    public static readonly IReadOnlyList<(string Name, string Unit)> Defaults =
        [("Poids", "kg"), ("Tour de taille", "cm"), ("Fréquence cardiaque au repos", "bpm")];

    public Guid HouseholdId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public string Name { get; private set; } = "";
    public string Unit { get; private set; } = "";
    public bool IsArchived { get; private set; }

    private MeasureType() { }

    public static MeasureType Create(Guid householdId, Guid ownerUserId, Guid memberProfileId, string name, string unit)
    {
        var type = new MeasureType { Id = Guid.NewGuid(), HouseholdId = householdId, OwnerUserId = ownerUserId, MemberProfileId = memberProfileId };
        type.Update(name, unit);
        return type;
    }

    public void Update(string name, string unit)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100) throw new ArgumentException("Nom de la mesure obligatoire (100 caractères maximum).");
        if (string.IsNullOrWhiteSpace(unit) || unit.Trim().Length > 30) throw new ArgumentException("Unité obligatoire (30 caractères maximum).");
        Name = name.Trim();
        Unit = unit.Trim();
    }

    public void Archive() => IsArchived = true;
    public void Restore() => IsArchived = false;
}

/// <summary>One body measure on a day.</summary>
public sealed class Measurement : Entity
{
    public Guid HouseholdId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public Guid MeasureTypeId { get; private set; }
    public DateOnly Day { get; private set; }
    public decimal Value { get; private set; }
    public string? Note { get; private set; }

    private Measurement() { }

    public static Measurement Create(MeasureType type, DateOnly day, decimal value, string? note, DateOnly today)
    {
        if (day > today) throw new ArgumentException("Une mesure ne peut pas être dans le futur.");
        if (value is < -1_000_000 or > 1_000_000) throw new ArgumentException("Valeur hors limites.");
        var text = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (text?.Length > 500) throw new ArgumentException("Note : 500 caractères maximum.");
        return new Measurement
        {
            Id = Guid.NewGuid(), HouseholdId = type.HouseholdId, OwnerUserId = type.OwnerUserId, MemberProfileId = type.MemberProfileId,
            MeasureTypeId = type.Id, Day = day, Value = value, Note = text,
        };
    }
}
