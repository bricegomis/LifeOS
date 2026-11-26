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

/// <param name="Kind">physique or mental.</param>
public sealed record SymptomTypeDto(Guid Id, string Name, string Kind, bool IsArchived)
{
    public static SymptomTypeDto From(SymptomType t) => new(t.Id, t.Name, t.Kind, t.IsArchived);
}

public sealed record SymptomEntryDto(Guid Id, Guid SymptomTypeId, string SymptomName, string Kind, DateOnly Day, TimeOnly? Time, int Intensity, string? Note);

/// <param name="Calm">Noted as "rien à signaler".</param>
/// <param name="MaxIntensity">The strongest symptom of the day; null without any.</param>
public sealed record SymptomDayDto(DateOnly Day, bool Calm, int? MaxIntensity, IReadOnlyList<SymptomEntryDto> Entries);

/// <summary>The journal from <paramref name="From"/> to <paramref name="Today"/>, newest day first, and the symptom list.</summary>
public sealed record SymptomJournalDto(DateOnly Today, DateOnly From, IReadOnlyList<SymptomDayDto> Days, IReadOnlyList<SymptomTypeDto> Types);

public sealed record MarkerValueDto(Guid AnalysisId, DateOnly Day, decimal Value, string Status);

/// <param name="FrequencyMonths">0: once.</param>
/// <param name="Status">Of the last value: below_lab, above_lab, in_target or normal (in the lab range, outside the target).</param>
/// <param name="Trend">From the previous value: better, worse, stable or in_target; null with less than two values.</param>
/// <param name="NextDueOn">When to measure it again; today when never measured, null when once is enough.</param>
/// <param name="Due">To measure within 30 days, or late.</param>
/// <param name="Values">Oldest first.</param>
/// <param name="Description">What it measures and what it is used for.</param>
/// <param name="WhenLow">What a value below the range can mean.</param>
/// <param name="WhenHigh">What a value above the range can mean.</param>
public sealed record MarkerDto(Guid Id, string Name, string Unit, decimal? LabMin, decimal? LabMax, decimal? TargetMin, decimal? TargetMax,
    int FrequencyMonths, IReadOnlyList<string> Panels, bool IsArchived, IReadOnlyList<MarkerValueDto> Values, string? Status, string? Trend,
    DateOnly? NextDueOn, bool Due, bool Late, string? Description, string? WhenLow, string? WhenHigh)
{
    public static MarkerDto From(Marker m, IEnumerable<Analysis> analyses, DateOnly today)
    {
        var values = analyses.SelectMany(a => a.Values.Where(v => v.MarkerId == m.Id).Select(v => (a.Id, a.Day, v.Value)))
            .OrderBy(v => v.Day).Select(v => new MarkerValueDto(v.Id, v.Day, v.Value, m.StatusOf(v.Value))).ToList();
        var last = values.LastOrDefault();
        var previous = values.Count > 1 ? values[^2].Value : (decimal?)null;
        var next = m.NextDueOn(last?.Day, today);
        return new(m.Id, m.Name, m.Unit, m.LabMin, m.LabMax, m.TargetMin, m.TargetMax, m.FrequencyMonths, m.Panels, m.IsArchived, values,
            last?.Status, last is null ? null : m.TrendOf(previous, last.Value), next, !m.IsArchived && next <= today.AddDays(30), !m.IsArchived && next < today,
            m.Description, m.WhenLow, m.WhenHigh);
    }
}

/// <param name="Panels">Every panel name used by the markers.</param>
public sealed record MarkerListDto(DateOnly Today, IReadOnlyList<MarkerDto> Markers, IReadOnlyList<string> Panels);

public sealed record AnalysisValueDto(Guid MarkerId, string MarkerName, string Unit, decimal Value, string Status);

public sealed record AnalysisDto(Guid Id, DateOnly Day, string? Laboratory, string? Notes, Guid? DocumentId, IReadOnlyList<AnalysisValueDto> Values)
{
    public static AnalysisDto From(Analysis a, IReadOnlyDictionary<Guid, Marker> markers) =>
        new(a.Id, a.Day, a.Laboratory, a.Notes, a.DocumentId, a.Values.Where(v => markers.ContainsKey(v.MarkerId))
            .Select(v => { var m = markers[v.MarkerId]; return new AnalysisValueDto(m.Id, m.Name, m.Unit, v.Value, m.StatusOf(v.Value)); })
            .OrderBy(v => v.MarkerName).ToList());
}

public sealed record MeasurementDto(Guid Id, DateOnly Day, decimal Value, string? Note);

/// <param name="Values">Oldest first.</param>
public sealed record MeasureTypeDto(Guid Id, string Name, string Unit, bool IsArchived, IReadOnlyList<MeasurementDto> Values);

public sealed record HealthDocumentFileDto(Guid Id, string ContentType, long Size, string? OriginalName);

/// <param name="Type">ordonnance, compte_rendu, resultat, bilan or autre.</param>
/// <param name="AnalysisIds">Analyses entered from this document.</param>
public sealed record HealthDocumentDto(Guid Id, string Type, string Title, DateOnly Day, string? Who, string? Notes, IReadOnlyList<HealthDocumentFileDto> Files,
    IReadOnlyList<Guid> AnalysisIds)
{
    public static HealthDocumentDto From(HealthDocument d, IEnumerable<Guid> analysisIds) =>
        new(d.Id, d.Type, d.Title, d.Day, d.Who, d.Notes, d.OrderedFiles.Select(f => new HealthDocumentFileDto(f.Id, f.ContentType, f.Size, f.OriginalName)).ToList(),
            analysisIds.ToList());
}

/// <param name="Time">"HH:mm:ss" when given.</param>
/// <param name="ReportTitle">Title of the report document, when linked.</param>
public sealed record AppointmentDto(Guid Id, DateOnly Day, TimeOnly? Time, string Who, string? Reason, string? Place, string? Notes, string? BookingUrl,
    Guid? ReportDocumentId, string? ReportTitle);

/// <param name="Upcoming">From today, soonest first.</param>
/// <param name="Past">Most recent first.</param>
public sealed record AppointmentListDto(DateOnly Today, IReadOnlyList<AppointmentDto> Upcoming, IReadOnlyList<AppointmentDto> Past);
