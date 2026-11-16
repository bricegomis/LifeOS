using LifeOS.Domain.Common;

namespace LifeOS.Domain.Sports;

/// <summary>One exercise of a routine with its target of the moment: "2 × 45 s à 10 kg".</summary>
public sealed record RoutineLineInput(Exercise Exercise, int Sets, int Target, decimal? LoadKg);

/// <summary>
/// A daily routine of a profile (ADR 0014): an ordered list of exercises with their targets, done
/// on its days of the week. Its days, jokers and exercises change from a given day, so past days
/// are judged with the routine they had. Done when every exercise of the day is ticked.
/// </summary>
public sealed class Routine : Entity
{
    public const int MaxJokersPerWeek = 2;
    public const int MaxLines = 30;
    /// <summary>Every day: bit 0 is Monday, bit 6 is Sunday.</summary>
    public const int EveryDay = 127;

    private readonly List<RoutineVersion> _versions = [];

    public Guid HouseholdId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public string Name { get; private set; } = "";
    public DateOnly StartedOn { get; private set; }
    /// <summary>Last day counted; null while the routine runs.</summary>
    public DateOnly? StoppedOn { get; private set; }
    public IReadOnlyList<RoutineVersion> Versions => _versions;

    private Routine() { }

    public static Routine Create(Guid householdId, Guid memberProfileId, string name, int days, int jokersPerWeek,
        IReadOnlyList<RoutineLineInput> lines, DateOnly today)
    {
        if (householdId == Guid.Empty || memberProfileId == Guid.Empty) throw new ArgumentException("Foyer et profil obligatoires.");
        var routine = new Routine { Id = Guid.NewGuid(), HouseholdId = householdId, MemberProfileId = memberProfileId, StartedOn = today };
        routine.Rename(name);
        routine._versions.Add(RoutineVersion.Create(householdId, today, days, jokersPerWeek, lines));
        return routine;
    }

    public bool IsActive => StoppedOn is null;

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            throw new ArgumentException("Nom de la routine obligatoire (100 caractères maximum).");
        Name = name.Trim();
    }

    /// <summary>The version in force on a day (the first one before the start).</summary>
    public RoutineVersion VersionOn(DateOnly day) =>
        _versions.Where(version => version.ValidFrom <= day).MaxBy(version => version.ValidFrom) ?? _versions.MinBy(version => version.ValidFrom)!;

    /// <summary>New days, jokers, exercises or targets apply from today; past days keep theirs.</summary>
    public void Change(int days, int jokersPerWeek, IReadOnlyList<RoutineLineInput> lines, DateOnly today)
    {
        if (!IsActive) throw new ArgumentException("Cette routine est arrêtée.");
        var changed = RoutineVersion.Create(HouseholdId, today, days, jokersPerWeek, lines);
        var current = VersionOn(today);
        if (current.SameAs(changed)) return;
        // A version already starting today is edited in place (same key), otherwise a new one starts today.
        if (current.ValidFrom == today) current.CopyFrom(changed);
        else _versions.Add(changed);
    }

    /// <summary>Stops counting after today; the history stays.</summary>
    public void Stop(DateOnly today)
    {
        if (IsActive) StoppedOn = today < StartedOn ? StartedOn : today;
    }

    /// <summary>
    /// Ticks an exercise on a planned day. Without values, the target of that day is recorded; a
    /// lower value still counts: ticking validates, the numbers are for the follow-up.
    /// </summary>
    public (int Value, decimal? LoadKg) CheckCompletion(DateOnly day, Exercise exercise, int? value, decimal? loadKg, DateOnly today)
    {
        if (!IsActive) throw new ArgumentException("Cette routine est arrêtée.");
        if (day > today) throw new ArgumentException("Impossible de cocher un exercice dans le futur.");
        if (day < StartedOn) throw new ArgumentException("Ce jour est avant le début de la routine.");
        var version = VersionOn(day);
        if (!version.IsPlanned(day)) throw new ArgumentException("La routine n'est pas prévue ce jour-là.");
        var line = version.Lines.SingleOrDefault(l => l.ExerciseId == exercise.Id)
            ?? throw new ArgumentException("Cet exercice ne fait pas partie de la routine ce jour-là.");
        var done = value ?? line.Target;
        return (RoutineLine.CheckTarget(done), exercise.WithLoad ? RoutineLine.CheckLoad(loadKg ?? line.LoadKg ?? 0) : null);
    }
}

/// <summary>Days, jokers and exercises of a routine from a day on.</summary>
public sealed class RoutineVersion
{
    private readonly List<RoutineLine> _lines = [];

    public DateOnly ValidFrom { get; private set; }
    /// <summary>Days of the week as bits, Monday first; <see cref="Routine.EveryDay"/> by default.</summary>
    public int Days { get; private set; }
    public int JokersPerWeek { get; private set; }
    public IReadOnlyList<RoutineLine> Lines => _lines;

    private RoutineVersion() { }

    internal static RoutineVersion Create(Guid householdId, DateOnly validFrom, int days, int jokersPerWeek, IReadOnlyList<RoutineLineInput> lines)
    {
        if (days is < 1 or > Routine.EveryDay) throw new ArgumentException("Choisissez au moins un jour de la semaine.");
        if (jokersPerWeek is < 0 or > Routine.MaxJokersPerWeek) throw new ArgumentException("Jokers par semaine : 0 à 2.");
        if (lines.Count is < 1 or > Routine.MaxLines) throw new ArgumentException("Une routine a 1 à 30 exercices.");
        if (lines.Select(line => line.Exercise.Id).Distinct().Count() != lines.Count)
            throw new ArgumentException("Un exercice ne figure qu'une fois dans une routine.");
        var version = new RoutineVersion { ValidFrom = validFrom, Days = days, JokersPerWeek = jokersPerWeek };
        version._lines.AddRange(lines.Select((line, index) => RoutineLine.Create(householdId, line, index)));
        return version;
    }

    public bool IsPlanned(DateOnly day) => (Days & DayBit(day)) != 0;

    /// <summary>Bit of a day of the week: 1 for Monday, 64 for Sunday.</summary>
    public static int DayBit(DateOnly day) => 1 << (((int)day.DayOfWeek + 6) % 7);

    public IReadOnlyList<RoutineLine> OrderedLines => _lines.OrderBy(line => line.Position).ToList();

    internal bool SameAs(RoutineVersion other) =>
        Days == other.Days && JokersPerWeek == other.JokersPerWeek &&
        OrderedLines.Select(line => (line.ExerciseId, line.Sets, line.Target, line.LoadKg))
            .SequenceEqual(other.OrderedLines.Select(line => (line.ExerciseId, line.Sets, line.Target, line.LoadKg)));

    /// <summary>Lines are updated in place, by exercise, so a kept exercise keeps its row.</summary>
    internal void CopyFrom(RoutineVersion other)
    {
        Days = other.Days;
        JokersPerWeek = other.JokersPerWeek;
        _lines.RemoveAll(line => other._lines.All(o => o.ExerciseId != line.ExerciseId));
        foreach (var changed in other._lines)
        {
            if (_lines.SingleOrDefault(line => line.ExerciseId == changed.ExerciseId) is { } kept) kept.CopyFrom(changed);
            else _lines.Add(changed);
        }
    }
}

public sealed class RoutineLine
{
    public Guid HouseholdId { get; private set; }
    public Guid ExerciseId { get; private set; }
    public int Position { get; private set; }
    public int Sets { get; private set; }
    /// <summary>Seconds or repetitions per set, following the exercise's measure.</summary>
    public int Target { get; private set; }
    /// <summary>Null when the exercise has no load.</summary>
    public decimal? LoadKg { get; private set; }

    private RoutineLine() { }

    internal static RoutineLine Create(Guid householdId, RoutineLineInput input, int position)
    {
        if (input.Exercise.HouseholdId != householdId) throw new ArgumentException("Exercice introuvable dans votre foyer.");
        if (input.Sets is < 1 or > 20) throw new ArgumentException("Séries : 1 à 20.");
        return new RoutineLine
        {
            HouseholdId = householdId, ExerciseId = input.Exercise.Id, Position = position, Sets = input.Sets,
            Target = CheckTarget(input.Target),
            LoadKg = input.Exercise.WithLoad ? CheckLoad(input.LoadKg ?? 0) : null,
        };
    }

    internal void CopyFrom(RoutineLine other)
    {
        Position = other.Position;
        Sets = other.Sets;
        Target = other.Target;
        LoadKg = other.LoadKg;
    }

    public static int CheckTarget(int value) =>
        value is < 1 or > 10000 ? throw new ArgumentException("Valeur (secondes ou répétitions) : 1 à 10000.") : value;

    /// <summary>A load in kg, by steps of 0.5.</summary>
    public static decimal CheckLoad(decimal loadKg) =>
        loadKg is < 0 or > 500 || loadKg * 2 != decimal.Truncate(loadKg * 2)
            ? throw new ArgumentException("Charge : 0 à 500 kg, par pas de 0,5.")
            : loadKg;
}

/// <summary>An exercise ticked in a routine on a day, with what was really done. One per exercise and per day.</summary>
public sealed class ExerciseCompletion : Entity
{
    public Guid HouseholdId { get; private set; }
    public Guid RoutineId { get; private set; }
    public Guid ExerciseId { get; private set; }
    public DateOnly Date { get; private set; }
    public int Value { get; private set; }
    public decimal? LoadKg { get; private set; }
    public DateTimeOffset LoggedAt { get; private set; }

    private ExerciseCompletion() { }

    public static ExerciseCompletion Create(Routine routine, Exercise exercise, DateOnly day, int? value, decimal? loadKg,
        DateOnly today, DateTimeOffset now)
    {
        var done = routine.CheckCompletion(day, exercise, value, loadKg, today);
        return new ExerciseCompletion
        {
            Id = Guid.NewGuid(), HouseholdId = routine.HouseholdId, RoutineId = routine.Id, ExerciseId = exercise.Id,
            Date = day, Value = done.Value, LoadKg = done.LoadKg, LoggedAt = now,
        };
    }

    /// <summary>Corrects what was done that day.</summary>
    public void Change(Routine routine, Exercise exercise, int? value, decimal? loadKg, DateOnly today, DateTimeOffset now)
    {
        (Value, LoadKg) = routine.CheckCompletion(Date, exercise, value, loadKg, today);
        LoggedAt = now;
    }
}
