using LifeOS.Domain.Sports;

namespace LifeOS.Domain.Tests.Sports;

public sealed class RoutineTests
{
    private static readonly Guid Household = Guid.NewGuid();
    private static readonly Guid Profile = Guid.NewGuid();
    // Monday 2026-11-16.
    private static readonly DateOnly Monday = new(2026, 11, 16);
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);
    private const int TuesdayThursdaySaturday = 2 | 8 | 32;

    private static readonly Exercise WallSit = Exercise.Create(Household, "Chaise contre le mur", "Dos plaqué au mur.", "hold", 10);
    private static readonly Exercise Bridge = Exercise.Create(Household, "Pont fessier", null, "reps", null);

    private static Routine Tendons(DateOnly start, int days = Routine.EveryDay, int jokers = 0) =>
        Routine.Create(Household, Profile, "Tendons", days, jokers,
            [new RoutineLineInput(WallSit, 2, 45, 10), new RoutineLineInput(Bridge, 2, 12, null)], start);

    /// <summary>One pass per day with that many exercises fully done (in line order).</summary>
    private static Dictionary<DateOnly, IReadOnlyList<IReadOnlyDictionary<Guid, int>>> Done(DateOnly from, params int[] counts) =>
        counts.Select((count, i) => (Day: from.AddDays(i), count)).Where(d => d.count > 0)
            .ToDictionary(d => d.Day, d => Passes(d.count == 1 ? Sets((WallSit, 2)) : Sets((WallSit, 2), (Bridge, 2))));

    private static IReadOnlyDictionary<Guid, int> Sets(params (Exercise Exercise, int Sets)[] ticks) =>
        ticks.ToDictionary(t => t.Exercise.Id, t => t.Sets);

    private static IReadOnlyList<IReadOnlyDictionary<Guid, int>> Passes(params IReadOnlyDictionary<Guid, int>[] passes) => passes;

    [Fact]
    public void An_exercise_keeps_its_measure_once_done_and_validates_its_fields()
    {
        var exercise = Exercise.Create(Household, "  Gainage  ", "  ", "hold", null);
        Assert.Equal("Gainage", exercise.Name);
        Assert.Null(exercise.Instructions);
        exercise.Update("Gainage latéral", "Bassin haut.", "reps", 5, alreadyDone: false);
        Assert.Equal(("reps", 5m), (exercise.Measure, exercise.DefaultLoadKg));
        Assert.Throws<ArgumentException>(() => exercise.Update("Gainage latéral", null, "hold", null, alreadyDone: true));
        exercise.Update("Gainage latéral", null, "reps", 0, alreadyDone: true);
        Assert.Null(exercise.DefaultLoadKg);
        Assert.Throws<ArgumentException>(() => exercise.Update("Gainage latéral", null, "reps", 2.3m, alreadyDone: true));
        Assert.Throws<ArgumentException>(() => Exercise.Create(Household, "Gainage", null, "minutes", null));
        Assert.Throws<ArgumentException>(() => Exercise.Create(Household, " ", null, "hold", null));
    }

    [Fact]
    public void A_routine_needs_days_exercises_once_and_loads_by_half_kilos()
    {
        var routine = Tendons(Monday);
        var lines = routine.VersionOn(Monday).OrderedLines;
        Assert.Equal([WallSit.Id, Bridge.Id], lines.Select(line => line.ExerciseId));
        Assert.Equal(10m, lines[0].LoadKg);
        Assert.Null(lines[1].LoadKg);
        // Without a load, a line takes the exercise's usual load; 0 means bodyweight.
        var defaults = Routine.Create(Household, Profile, "Défaut", Routine.EveryDay, 0,
            [new RoutineLineInput(WallSit, 1, 45, null), new RoutineLineInput(Bridge, 1, 10, 0)], Monday).VersionOn(Monday).OrderedLines;
        Assert.Equal(new decimal?[] { 10, null }, defaults.Select(line => line.LoadKg));

        Assert.Throws<ArgumentException>(() => Routine.Create(Household, Profile, "Vide", Routine.EveryDay, 0, [], Monday));
        Assert.Throws<ArgumentException>(() => Routine.Create(Household, Profile, "Sans jour", 0, 0, [new RoutineLineInput(Bridge, 1, 10, null)], Monday));
        Assert.Throws<ArgumentException>(() => Routine.Create(Household, Profile, "Doublon", Routine.EveryDay, 0,
            [new RoutineLineInput(Bridge, 1, 10, null), new RoutineLineInput(Bridge, 2, 10, null)], Monday));
        Assert.Throws<ArgumentException>(() => Routine.Create(Household, Profile, "Charge", Routine.EveryDay, 0,
            [new RoutineLineInput(WallSit, 1, 45, 10.2m)], Monday));
        var stranger = Exercise.Create(Guid.NewGuid(), "Ailleurs", null, "reps", null);
        Assert.Throws<ArgumentException>(() => Routine.Create(Household, Profile, "Autre foyer", Routine.EveryDay, 0,
            [new RoutineLineInput(stranger, 1, 10, null)], Monday));
    }

    [Fact]
    public void A_change_applies_from_today_and_past_days_keep_their_routine()
    {
        var start = Monday.AddDays(-3);
        var routine = Tendons(start);
        routine.Change(Routine.EveryDay, 1, [new RoutineLineInput(WallSit, 2, 60, 12)], Monday);
        Assert.Equal(2, routine.Versions.Count);
        Assert.Equal(2, routine.VersionOn(Monday.AddDays(-1)).Lines.Count);
        Assert.Equal(60, Assert.Single(routine.VersionOn(Monday).Lines).Target);

        // A second change the same day edits today's version in place.
        routine.Change(Routine.EveryDay, 1, [new RoutineLineInput(Bridge, 3, 15, null), new RoutineLineInput(WallSit, 2, 60, 12.5m)], Monday);
        Assert.Equal(2, routine.Versions.Count);
        Assert.Equal([Bridge.Id, WallSit.Id], routine.VersionOn(Monday).OrderedLines.Select(line => line.ExerciseId));
        routine.Change(Routine.EveryDay, 1, [new RoutineLineInput(Bridge, 3, 15, null), new RoutineLineInput(WallSit, 2, 60, 12.5m)], Monday.AddDays(1));
        Assert.Equal(2, routine.Versions.Count);
    }

    [Fact]
    public void A_pass_is_ticked_set_by_set_with_the_target_and_refuses_unplanned_or_future_days()
    {
        var routine = Tendons(Monday.AddDays(-1));
        var pass = RoutineSession.Start(routine, Monday, Now, Monday, hasOpenPass: false);
        var tick = ExerciseCompletion.Create(routine, pass, WallSit.Id, 1, null, null, Now, passFinished: false);
        Assert.Equal((1, 45, 10m), (tick.SetsDone, tick.Value, tick.LoadKg));
        tick.SetSets(routine, 2, Now, passFinished: false);
        Assert.Equal(2, tick.SetsDone);
        Assert.Throws<ArgumentException>(() => tick.SetSets(routine, 3, Now, passFinished: false));
        Assert.Throws<ArgumentException>(() => tick.SetSets(routine, 1, Now, passFinished: true));
        // The API corrects what was really done; 0 kg is bodyweight.
        tick.Correct(routine, 60, 0);
        Assert.Equal((60, (decimal?)null), (tick.Value, tick.LoadKg));
        Assert.Null(ExerciseCompletion.Create(routine, pass, Bridge.Id, 2, 8, null, Now, passFinished: false).LoadKg);

        var other = Exercise.Create(Household, "Superman", null, "hold", null);
        Assert.Throws<ArgumentException>(() => ExerciseCompletion.Create(routine, pass, other.Id, 1, null, null, Now, passFinished: false));
        Assert.Throws<ArgumentException>(() => ExerciseCompletion.Create(routine, pass, Bridge.Id, 1, null, null, Now, passFinished: true));
        Assert.Throws<ArgumentException>(() => RoutineSession.Start(routine, Monday.AddDays(1), Now, Monday, false));
        Assert.Throws<ArgumentException>(() => RoutineSession.Start(routine, Monday.AddDays(-2), Now, Monday, false));
        Assert.Throws<ArgumentException>(() => RoutineSession.Start(routine, Monday, Now, Monday, hasOpenPass: true));
        var alternate = Tendons(Monday.AddDays(-7), TuesdayThursdaySaturday);
        Assert.Throws<ArgumentException>(() => RoutineSession.Start(alternate, Monday, Now, Monday, false));
        routine.Stop(Monday);
        Assert.Throws<ArgumentException>(() => RoutineSession.Start(routine, Monday, Now, Monday, false));
    }

    [Fact]
    public void A_pass_is_finished_when_every_exercise_has_all_its_sets_and_carries_an_optional_feeling()
    {
        var routine = Tendons(Monday);
        var pass = RoutineSession.Start(routine, Monday, Now, Monday, false);
        var sit = ExerciseCompletion.Create(routine, pass, WallSit.Id, 2, null, null, Now, false);
        var bridge = ExerciseCompletion.Create(routine, pass, Bridge.Id, 1, null, null, Now, false);
        Assert.False(pass.IsFinished(routine, [sit, bridge]));
        bridge.SetSets(routine, 2, Now, false);
        Assert.True(pass.IsFinished(routine, [sit, bridge]));

        sit.SetFeeling(7, 0);
        Assert.Equal((7, 0), (sit.Effort, sit.Discomfort));
        sit.SetFeeling(null, null);
        Assert.Null(sit.Effort);
        Assert.Throws<ArgumentException>(() => sit.SetFeeling(0, null));
        Assert.Throws<ArgumentException>(() => sit.SetFeeling(null, 11));
        pass.SetComment("  Genou un peu raide.  ");
        Assert.Equal("Genou un peu raide.", pass.Comment);
        Assert.Throws<ArgumentException>(() => pass.SetComment(new string('a', 1001)));
    }

    [Fact]
    public void Profile_settings_offer_seven_and_two_until_changed()
    {
        var settings = SportProfileSettings.Create(Household, Profile);
        Assert.Equal((7, 2), (settings.DefaultEffort, settings.DefaultDiscomfort));
        settings.Change(6, 0);
        Assert.Equal((6, 0), (settings.DefaultEffort, settings.DefaultDiscomfort));
        Assert.Throws<ArgumentException>(() => settings.Change(0, 2));
        Assert.Throws<ArgumentException>(() => settings.Change(7, 11));
    }

    [Fact]
    public void The_routine_is_done_when_every_exercise_is_ticked_and_today_never_breaks_the_streak()
    {
        var start = Monday.AddDays(-3);
        var progress = RoutineProgress.Compute(Tendons(start), Done(start, 2, 2, 2, 1), Monday, start);
        Assert.Equal(3, progress.Streak);
        Assert.True(progress.PlannedToday);
        Assert.Equal((1, 2), (progress.DoneToday, progress.TotalToday));
        Assert.Equal(RoutineDayStatus.InProgress, progress.Calendar[^1].Status);
        Assert.Equal(50, progress.Calendar[^1].Percent);
        Assert.Equal(Monday.AddDays(1), progress.NextPlannedDay);
    }

    [Fact]
    public void One_finished_pass_makes_the_day_and_more_passes_are_only_counted()
    {
        var start = Monday.AddDays(-2);
        var routine = Tendons(start);
        var full = Sets((WallSit, 2), (Bridge, 2));
        var passes = new Dictionary<DateOnly, IReadOnlyList<IReadOnlyDictionary<Guid, int>>>
        {
            // Three passes, two finished; a pass with one set short is not finished.
            [start] = Passes(full, Sets((WallSit, 2), (Bridge, 1)), full),
            [start.AddDays(1)] = Passes(Sets((WallSit, 1), (Bridge, 2))),
            [Monday] = Passes(full, Sets((WallSit, 2))),
        };
        var progress = RoutineProgress.Compute(routine, passes, Monday, start);
        Assert.Equal(2, progress.Calendar[0].Passes);
        Assert.Equal(RoutineDayStatus.Done, progress.Calendar[0].Status);
        // The best pass of a day colours it: one exercise of two.
        Assert.Equal((RoutineDayStatus.Missed, 1, 0), (progress.Calendar[1].Status, progress.Calendar[1].Done, progress.Calendar[1].Passes));
        Assert.Equal(1, progress.Streak);
        Assert.Equal((2, 2, 1), (progress.DoneToday, progress.TotalToday, progress.PassesToday));
    }

    [Fact]
    public void A_half_done_day_restarts_the_streak_unless_a_joker_of_its_week_covers_it()
    {
        var start = Monday.AddDays(-5);
        var done = Done(start, 2, 2, 1, 0, 2, 2);
        var strict = RoutineProgress.Compute(Tendons(start), done, Monday, start);
        Assert.Equal(2, strict.Streak);
        Assert.Equal(2, strict.Record);
        Assert.Equal(RoutineDayStatus.Missed, strict.Calendar[2].Status);

        var lenient = RoutineProgress.Compute(Tendons(start, jokers: 1), done, Monday, start);
        Assert.Equal(RoutineDayStatus.Joker, lenient.Calendar[2].Status);
        Assert.Equal(RoutineDayStatus.Missed, lenient.Calendar[3].Status);
        Assert.Equal(2, lenient.Streak);
        Assert.Equal(1, lenient.JokersLeftThisWeek);
    }

    [Fact]
    public void Days_that_are_not_planned_neither_count_nor_break_the_streak()
    {
        // Tuesday 3 November: planned on Tuesdays, Thursdays and Saturdays.
        var start = Monday.AddDays(-13);
        var routine = Tendons(start, TuesdayThursdaySaturday);
        var done = new[] { 0, 2, 4, 7, 9, 11 }.ToDictionary(i => start.AddDays(i), _ => Passes(Sets((WallSit, 2), (Bridge, 2))));
        var progress = RoutineProgress.Compute(routine, done, Monday, start);
        Assert.Equal(6, progress.Streak);
        Assert.False(progress.PlannedToday);
        Assert.Equal(0, progress.TotalToday);
        Assert.Equal(RoutineDayStatus.NotPlanned, progress.Calendar[^1].Status);
        Assert.Equal(Monday.AddDays(1), progress.NextPlannedDay);
        Assert.Equal(RoutineDayStatus.NotCounted, RoutineProgress.Compute(routine, done, Monday, start.AddDays(-1)).Calendar[0].Status);
    }

    [Fact]
    public void A_past_day_is_judged_with_the_exercises_it_had()
    {
        var start = Monday.AddDays(-2);
        var routine = Tendons(start);
        var third = Exercise.Create(Household, "Gainage", null, "hold", null);
        routine.Change(Routine.EveryDay, 0,
            [new RoutineLineInput(WallSit, 2, 45, 10), new RoutineLineInput(Bridge, 2, 12, null), new RoutineLineInput(third, 1, 30, null)], Monday);
        var progress = RoutineProgress.Compute(routine, Done(start, 2, 2, 2), Monday, start);
        Assert.Equal(2, progress.Streak);
        Assert.Equal((2, 3), (progress.DoneToday, progress.TotalToday));
    }

    [Fact]
    public void A_stopped_routine_keeps_its_record_but_has_no_streak()
    {
        var start = Monday.AddDays(-4);
        var routine = Tendons(start);
        routine.Stop(Monday.AddDays(-2));
        var progress = RoutineProgress.Compute(routine, Done(start, 2, 2, 2), Monday, start);
        Assert.Equal(0, progress.Streak);
        Assert.Equal(3, progress.Record);
        Assert.False(progress.PlannedToday);
        Assert.Null(progress.NextPlannedDay);
        Assert.Equal(RoutineDayStatus.NotCounted, progress.Calendar[^1].Status);
    }

    [Fact]
    public void An_exercise_is_followed_by_its_best_value_and_load_of_each_week_never_combined()
    {
        var routine = Tendons(Monday.AddDays(-30));
        ExerciseCompletion Tick(int daysAgo, int value, decimal load)
        {
            var pass = RoutineSession.Start(routine, Monday.AddDays(-daysAgo), Now, Monday, false);
            return ExerciseCompletion.Create(routine, pass, WallSit.Id, 2, value, load, Now.AddDays(-daysAgo), false);
        }
        // 22 days ago is before the four weeks shown; 8 days ago is the Sunday of the week before last.
        var progress = ExerciseProgress.Compute([Tick(22, 70, 5), Tick(8, 60, 5), Tick(7, 45, 8), Tick(0, 45, 10)], Monday, 4);
        Assert.Equal(70, progress.BestValue);
        Assert.Equal(10m, progress.BestLoadKg);
        Assert.Equal(4, progress.DaysDone);
        Assert.Equal([Monday.AddDays(-21), Monday.AddDays(-14), Monday.AddDays(-7), Monday], progress.Weeks.Select(week => week.Monday));
        Assert.Equal(new int?[] { null, 60, 45, 45 }, progress.Weeks.Select(week => week.BestValue));
        Assert.Equal(new decimal?[] { null, 5, 8, 10 }, progress.Weeks.Select(week => week.BestLoadKg));
        Assert.Equal([Monday, Monday.AddDays(-7), Monday.AddDays(-8), Monday.AddDays(-22)], progress.Recent.Select(tick => tick.Date));

        var empty = ExerciseProgress.Compute([], Monday, 2);
        Assert.Null(empty.BestValue);
        Assert.Null(empty.BestLoadKg);
        Assert.Equal(2, empty.Weeks.Count);
        Assert.Empty(empty.Recent);
    }
}
