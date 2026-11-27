using LifeOS.Domain.Common;

namespace LifeOS.Domain.Sports;

/// <summary>
/// One pass of a routine on a day (ADR 0015): a routine can be done several times the same day. Its
/// ticks are <see cref="ExerciseCompletion"/> rows. Whether it is finished is never stored: a pass is
/// finished when every exercise of the routine that day has all its sets ticked.
/// </summary>
public sealed class RoutineSession : Entity
{
    public Guid HouseholdId { get; private set; }
    public Guid RoutineId { get; private set; }
    public DateOnly Date { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }

    private RoutineSession() { }

    /// <param name="hasOpenPass">The routine already has an unfinished pass that day: only one at a time.</param>
    public static RoutineSession Start(Routine routine, DateOnly day, DateTimeOffset startedAt, DateOnly today, bool hasOpenPass)
    {
        routine.CheckDay(day, today);
        if (hasOpenPass) throw new ArgumentException("Un passage de cette routine est déjà en cours ce jour-là ; terminez-le ou supprimez-le.");
        return new RoutineSession
        {
            Id = Guid.NewGuid(), HouseholdId = routine.HouseholdId, RoutineId = routine.Id, Date = day, StartedAt = startedAt,
        };
    }

    /// <summary>Exercises of the routine that day with all their sets ticked in this pass.</summary>
    public static int ExercisesDone(Routine routine, DateOnly day, IReadOnlyDictionary<Guid, int> setsByExercise) =>
        routine.VersionOn(day).Lines.Count(line => setsByExercise.GetValueOrDefault(line.ExerciseId) >= line.Sets);

    public bool IsFinished(Routine routine, IEnumerable<ExerciseCompletion> ticks)
    {
        var sets = ticks.Where(t => t.SessionId == Id).ToDictionary(t => t.ExerciseId, t => t.SetsDone);
        return ExercisesDone(routine, Date, sets) == routine.VersionOn(Date).Lines.Count;
    }
}

/// <summary>
/// An exercise ticked in a pass (ADR 0014, ADR 0015): how many of its sets are done, the value and load of
/// each set (the target unless corrected through the API). One per exercise and per pass.
/// </summary>
public sealed class ExerciseCompletion : Entity
{
    public Guid HouseholdId { get; private set; }
    public Guid RoutineId { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid ExerciseId { get; private set; }
    /// <summary>Day of the pass, kept for the follow-up reads.</summary>
    public DateOnly Date { get; private set; }
    public int SetsDone { get; private set; }
    public int Value { get; private set; }
    public decimal? LoadKg { get; private set; }
    public DateTimeOffset LoggedAt { get; private set; }

    private ExerciseCompletion() { }

    /// <param name="passFinished">The pass already has every exercise done: its sets no longer change.</param>
    public static ExerciseCompletion Create(Routine routine, RoutineSession session, Guid exerciseId, int setsDone, int? value,
        decimal? loadKg, DateTimeOffset now, bool passFinished)
    {
        if (session.RoutineId != routine.Id || session.HouseholdId != routine.HouseholdId) throw new ArgumentException("Passage introuvable pour cette routine.");
        if (passFinished) throw FinishedPass();
        var done = routine.CheckCompletion(session.Date, exerciseId, value, loadKg);
        var completion = new ExerciseCompletion
        {
            Id = Guid.NewGuid(), HouseholdId = routine.HouseholdId, RoutineId = routine.Id, SessionId = session.Id, ExerciseId = exerciseId,
            Date = session.Date, Value = done.Value, LoadKg = done.LoadKg, LoggedAt = now,
        };
        completion.SetSets(routine, setsDone, now, passFinished: false);
        return completion;
    }

    /// <summary>Sets done, 1 to the sets of the target; a finished pass is not reopened (delete it and do it again).</summary>
    public void SetSets(Routine routine, int setsDone, DateTimeOffset now, bool passFinished)
    {
        if (passFinished) throw FinishedPass();
        var sets = routine.LineOn(Date, ExerciseId).Sets;
        if (setsDone < 1 || setsDone > sets) throw new ArgumentException($"Séries faites : 1 à {sets}.");
        SetsDone = setsDone;
        LoggedAt = now;
    }

    /// <summary>Corrects the value and load really done (API and assistant only).</summary>
    public void Correct(Routine routine, int? value, decimal? loadKg)
    {
        var done = routine.CheckCompletion(Date, ExerciseId, value ?? Value, loadKg ?? LoadKg ?? 0);
        (Value, LoadKg) = done;
    }

    private static ArgumentException FinishedPass() =>
        new("Ce passage est terminé ; supprimez-le pour le refaire.");
}
