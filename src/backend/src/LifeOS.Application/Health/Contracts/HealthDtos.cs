using LifeOS.Domain.Health;

namespace LifeOS.Application.Health.Contracts;

public sealed record SupplementLinkDto(string Label, string Url);

/// <param name="Kind">year or cure.</param>
/// <param name="CureUnit">week or month (a week is 7 days, a month 30).</param>
/// <param name="PauseLength">0: a single cure.</param>
public sealed record SupplementPeriodDto(string Kind, DateOnly StartOn, int CureLength, string CureUnit, int PauseLength, string PauseUnit)
{
    public static SupplementPeriodDto From(SupplementPeriod p) => new(p.Kind, p.StartOn, p.CureLength, p.CureUnit, p.PauseLength, p.PauseUnit);
}

public sealed record SupplementStatusDto(bool Active, int? DayInCure, int? CureDays, int? DaysLeft, DateOnly? NextCureOn, bool Ended);

public sealed record SupplementDayDto(DateOnly Day, bool Taken, bool Active);

/// <param name="Unit">gelule, comprime, g, ml, goutte or dose.</param>
/// <param name="Moment">matin, midi, soir or choix.</param>
/// <param name="MonthlyCost">price × dose × 30 / contents, for the share of days in cure; null without a price.</param>
/// <param name="Status">Where it stands today (in cure, in pause, ended).</param>
/// <param name="Streak">Days taken in a row up to today; days out of cure are skipped.</param>
/// <param name="RecentDays">The last 14 days, oldest first.</param>
public sealed record SupplementDto(Guid Id, string Name, string? Notes, decimal Dose, string Unit, string Moment, decimal PackageContent,
    decimal? PackagePrice, string? Shop, IReadOnlyList<SupplementLinkDto> Links, SupplementPeriodDto Period, bool IsStopped, bool HasImage,
    DateTimeOffset UpdatedAt, decimal? MonthlyCost, SupplementStatusDto Status, bool TakenToday, int Streak, IReadOnlyList<SupplementDayDto> RecentDays)
{
    public static SupplementDto From(Supplement s, DateOnly today)
    {
        var status = s.Period.StatusOn(today);
        return new(s.Id, s.Name, s.Notes, s.Dose, s.Unit, s.Moment, s.PackageContent, s.PackagePrice, s.Shop,
            s.OrderedLinks.Select(l => new SupplementLinkDto(l.Label, l.Url)).ToList(), SupplementPeriodDto.From(s.Period), s.IsStopped, s.HasImage,
            s.UpdatedAt, s.MonthlyCost,
            new SupplementStatusDto(status.Active && !s.IsStopped, status.DayInCure, status.CureDays, status.DaysLeft, status.NextCureOn, status.Ended),
            s.TakenOn(today), s.Streak(today),
            Enumerable.Range(0, 14).Select(i => today.AddDays(i - 13))
                .Select(day => new SupplementDayDto(day, s.TakenOn(day), !s.IsStopped && s.Period.StatusOn(day).Active)).ToList());
    }
}

/// <param name="MonthlyTotal">Sum of the monthly costs of the supplements that are not stopped (known prices only).</param>
public sealed record SupplementListDto(DateOnly Today, decimal MonthlyTotal, IReadOnlyList<SupplementDto> Supplements);
