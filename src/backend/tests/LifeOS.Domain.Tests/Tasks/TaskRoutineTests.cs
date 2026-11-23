using LifeOS.Domain.Tasks;

namespace LifeOS.Domain.Tests.Tasks;

public sealed class TaskRoutineTests
{
    private static readonly Guid Household = Guid.NewGuid();
    private static readonly Guid Profile = Guid.NewGuid();
    private static readonly DateOnly Sunday = new(2026, 11, 22);
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);

    private static TaskRoutine Create(Recurrence recurrence, DateOnly start) =>
        TaskRoutine.Create(Household, Profile, " Sortir les poubelles ", null, 5, null, ["maison"], recurrence, start, Now);

    private static RoutineStatus Status(TaskRoutine routine, DateOnly today) => RoutineSchedule.Compute(routine, today);

    [Fact]
    public void Fixed_dates_follow_days_weeks_and_months()
    {
        var start = new DateOnly(2026, 11, 2); // a Monday
        Assert.Equal(new DateOnly(2026, 11, 5), Recurrence.FixedDays(3).FirstOnOrAfter(new DateOnly(2026, 11, 4), start));
        // Monday and Thursday, every other week.
        var weekly = Recurrence.Weekly(2, [1, 4]);
        Assert.Equal(new DateOnly(2026, 11, 5), weekly.FirstOnOrAfter(new DateOnly(2026, 11, 3), start));
        Assert.Equal(new DateOnly(2026, 11, 16), weekly.FirstOnOrAfter(new DateOnly(2026, 11, 6), start));
        // The 31st falls on the last day of a shorter month.
        var monthly = Recurrence.Monthly(1, 31);
        Assert.Equal(new DateOnly(2026, 11, 30), monthly.FirstOnOrAfter(new DateOnly(2026, 11, 2), start));
        Assert.Equal(new DateOnly(2027, 2, 28), monthly.FirstOnOrAfter(new DateOnly(2027, 2, 1), start));
        Assert.Equal(new DateOnly(2027, 1, 25), Recurrence.Monthly(2, 25).FirstOnOrAfter(new DateOnly(2026, 11, 26), start));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    public void A_recurrence_stays_within_bounds(int value)
    {
        Assert.Throws<ArgumentException>(() => Recurrence.FixedDays(value));
        Assert.Throws<ArgumentException>(() => Recurrence.Delay(value));
    }

    [Fact]
    public void A_weekly_recurrence_needs_a_day() =>
        Assert.Throws<ArgumentException>(() => Recurrence.Weekly(1, []));

    [Fact]
    public void Late_due_days_never_pile_up_and_a_late_pass_resets_the_streak()
    {
        // Every Monday and Thursday from Monday 2 November.
        var routine = Create(Recurrence.Weekly(1, [1, 4]), new DateOnly(2026, 11, 2));
        Assert.Equal(new DateOnly(2026, 11, 2), Status(routine, new DateOnly(2026, 11, 2)).NextDueOn);
        routine.Complete(new DateOnly(2026, 11, 2), null, new DateOnly(2026, 11, 2), Now);
        routine.Complete(new DateOnly(2026, 11, 5), "Jaune", new DateOnly(2026, 11, 5), Now);
        var status = Status(routine, new DateOnly(2026, 11, 20));
        Assert.Equal((new DateOnly(2026, 11, 9), true, 2, 2), (status.NextDueOn, status.IsLate, status.Streak, status.Record));

        // Done ten days late: the streak restarts, the next due day comes after the pass.
        routine.Complete(new DateOnly(2026, 11, 19), null, new DateOnly(2026, 11, 20), Now);
        status = Status(routine, new DateOnly(2026, 11, 20));
        Assert.Equal((new DateOnly(2026, 11, 23), false, 0, 2), (status.NextDueOn, status.IsLate, status.Streak, status.Record));
        Assert.Equal(new DateOnly(2026, 11, 19), status.LastDoneOn);
    }

    [Fact]
    public void A_delay_routine_counts_from_the_last_pass_and_skipping_resets_the_streak()
    {
        var routine = Create(Recurrence.Delay(7), new DateOnly(2026, 11, 1));
        routine.Complete(new DateOnly(2026, 11, 1), null, new DateOnly(2026, 11, 1), Now);
        routine.Complete(new DateOnly(2026, 11, 6), null, new DateOnly(2026, 11, 6), Now);
        Assert.Equal((new DateOnly(2026, 11, 13), 2), (Status(routine, Sunday).NextDueOn!.Value, Status(routine, Sunday).Streak));
        routine.Skip(Sunday, Now);
        var status = Status(routine, Sunday);
        Assert.Equal((new DateOnly(2026, 11, 29), 0, 2, false), (status.NextDueOn!.Value, status.Streak, status.Record, status.DoneToday));
    }

    [Fact]
    public void Removing_a_pass_recomputes_the_due_day()
    {
        var routine = Create(Recurrence.Delay(4), Sunday);
        var pass = routine.Complete(Sunday, null, Sunday, Now);
        Assert.True(Status(routine, Sunday).DoneToday);
        Assert.Equal(Sunday.AddDays(4), Status(routine, Sunday).NextDueOn);
        routine.RemovePass(pass.Id, Now);
        Assert.Equal(Sunday, Status(routine, Sunday).NextDueOn);
    }

    [Fact]
    public void A_pause_has_no_due_day_and_resuming_never_makes_it_late()
    {
        var routine = Create(Recurrence.FixedDays(1), new DateOnly(2026, 11, 1));
        routine.Complete(new DateOnly(2026, 11, 1), null, new DateOnly(2026, 11, 1), Now);
        routine.Pause(Now);
        Assert.Null(Status(routine, Sunday).NextDueOn);
        Assert.Throws<ArgumentException>(() => routine.Complete(Sunday, null, Sunday, Now));
        routine.Resume(Sunday, Now);
        var status = Status(routine, Sunday);
        Assert.Equal((Sunday, false, 1), (status.NextDueOn!.Value, status.IsLate, status.Streak));
        routine.Stop(Now);
        Assert.Null(Status(routine, Sunday).NextDueOn);
        Assert.Throws<ArgumentException>(() => routine.Pause(Now));
    }

    [Fact]
    public void A_pass_is_never_in_the_future_nor_before_the_last_one()
    {
        var routine = Create(Recurrence.Delay(2), new DateOnly(2026, 11, 10));
        Assert.Throws<ArgumentException>(() => routine.Complete(Sunday.AddDays(1), null, Sunday, Now));
        routine.Complete(Sunday, null, Sunday, Now);
        Assert.Throws<ArgumentException>(() => routine.Complete(Sunday.AddDays(-1), null, Sunday, Now));
    }

    [Fact]
    public void Title_priority_and_tags_follow_the_todo_rules()
    {
        var routine = Create(Recurrence.Delay(7), Sunday);
        Assert.Equal(("Sortir les poubelles", "normal", "active"), (routine.Title, routine.Priority, routine.State));
        Assert.Throws<ArgumentException>(() => TaskRoutine.Create(Household, Profile, " ", null, null, null, null, Recurrence.Delay(1), Sunday, Now));
        Assert.Throws<ArgumentException>(() => TaskRoutine.Create(Household, Profile, "A", null, null, "urgent", null, Recurrence.Delay(1), Sunday, Now));
    }

    [Fact]
    public void Due_days_start_with_the_open_one_then_follow_fixed_dates_as_if_done_today()
    {
        // Monday and Thursday from 2 November, never done: the open due day is 2 November, late.
        var routine = Create(Recurrence.Weekly(1, [1, 4]), new DateOnly(2026, 11, 2));
        var monday = new DateOnly(2026, 11, 23);
        Assert.Equal([new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 26), new DateOnly(2026, 11, 30), new DateOnly(2026, 12, 3)],
            RoutineSchedule.DueDays(routine, monday, new DateOnly(2026, 12, 3)));
    }

    [Fact]
    public void Due_days_of_a_delay_routine_count_from_today_when_the_open_one_is_late()
    {
        var routine = Create(Recurrence.Delay(7), new DateOnly(2026, 11, 20));
        Assert.Equal([new DateOnly(2026, 11, 20), new DateOnly(2026, 11, 29), new DateOnly(2026, 12, 6)],
            RoutineSchedule.DueDays(routine, Sunday, new DateOnly(2026, 12, 10)));
        routine.Complete(Sunday, null, Sunday, Now);
        Assert.Equal([new DateOnly(2026, 11, 29), new DateOnly(2026, 12, 6)], RoutineSchedule.DueDays(routine, Sunday, new DateOnly(2026, 12, 10)));
    }

    [Fact]
    public void A_paused_routine_or_one_due_after_the_period_has_no_due_day()
    {
        var later = Create(Recurrence.Delay(7), new DateOnly(2026, 12, 1));
        Assert.Empty(RoutineSchedule.DueDays(later, Sunday, new DateOnly(2026, 11, 30)));
        var paused = Create(Recurrence.FixedDays(1), Sunday);
        paused.Pause(Now);
        Assert.Empty(RoutineSchedule.DueDays(paused, Sunday, new DateOnly(2026, 12, 31)));
    }
}
