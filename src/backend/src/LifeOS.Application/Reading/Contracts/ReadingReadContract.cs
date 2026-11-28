using LifeOS.Domain.Reading;

namespace LifeOS.Application.Reading.Contracts;

/// <summary>Reads the Reading read contract needs; implemented by Infrastructure on the reading tables.</summary>
public interface IReadingReadData
{
    /// <summary>Every book of the household with its readings.</summary>
    Task<IReadOnlyList<Book>> BooksAsync(Guid householdId, CancellationToken ct);
}

public sealed record ReadingInProgressDto(Guid BookId, Guid MemberProfileId, string Title, string Authors, string Format, DateOnly StartedOn);

/// <param name="Outcome">read or stopped.</param>
public sealed record ReadingEndedDto(DateOnly Date, Guid MemberProfileId, Guid BookId, string Title, string Outcome, int? Rating);

/// <summary>
/// Published read contract of the Reading module (ADR 0009, ADR 0022), for the future planner: books being read and
/// readings finished or stopped per day and per profile, computed on read.
/// </summary>
public sealed class ReadingReadContract(IReadingReadData data, TimeProvider clock)
{
    private const int MaxDays = 366;

    public DateOnly Today() => ReadingClock.Today(clock);

    /// <summary>Books with a reading in progress, the oldest started first.</summary>
    public async Task<IReadOnlyList<ReadingInProgressDto>> InProgressAsync(Guid householdId, CancellationToken ct) =>
        (await data.BooksAsync(householdId, ct))
            .Where(b => b.CurrentReading?.StartedOn is not null)
            .Select(b => new ReadingInProgressDto(b.Id, b.MemberProfileId, b.Title, b.Authors, b.Format, b.CurrentReading!.StartedOn!.Value))
            .OrderBy(r => r.StartedOn).ThenBy(r => r.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    /// <summary>Readings that ended (read or stopped) from <paramref name="from"/> to <paramref name="to"/> included, 366 days at most.</summary>
    public async Task<IReadOnlyList<ReadingEndedDto>> EndedAsync(Guid householdId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (to < from) throw new ArgumentException("La fin de la période vient après son début.");
        if (to.DayNumber - from.DayNumber >= MaxDays) throw new ArgumentException($"Une période fait au plus {MaxDays} jours.");
        return (await data.BooksAsync(householdId, ct))
            .SelectMany(b => b.Readings
                .Where(r => r.Outcome != "reading" && r.FinishedOn is { } day && day >= from && day <= to)
                .Select(r => new ReadingEndedDto(r.FinishedOn!.Value, b.MemberProfileId, b.Id, b.Title, r.Outcome, r.Rating)))
            .OrderBy(r => r.Date).ThenBy(r => r.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
