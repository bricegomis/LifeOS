using LifeOS.Domain.Common;

namespace LifeOS.Domain.Health;

/// <summary>
/// A food supplement taken by its owner (ADR 0018): a daily dose in a unit, a moment of the day for
/// ordering, a package (contents in the same unit, price paid, shop, purchase links), and a period:
/// all year, or a cure of N weeks or months that may repeat after a pause. One tick per day records
/// that it was taken, whatever the number of intakes that day. Its monthly cost is computed, never stored.
/// </summary>
public sealed class Supplement : Entity
{
    public static readonly IReadOnlyDictionary<string, string> Units = new Dictionary<string, string>
    {
        ["gelule"] = "gélule", ["comprime"] = "comprimé", ["g"] = "g", ["ml"] = "ml", ["goutte"] = "goutte", ["dose"] = "dose",
    };

    public static readonly IReadOnlyDictionary<string, string> Moments = new Dictionary<string, string>
    {
        ["matin"] = "Matin", ["midi"] = "Midi", ["soir"] = "Soir", ["choix"] = "Au choix",
    };

    public const int MaxNameLength = 200;
    public const int MaxNotesLength = 4000;
    public const int MaxLinks = 10;

    private readonly List<SupplementIntake> _intakes = [];
    private readonly List<SupplementLink> _links = [];

    public Guid HouseholdId { get; private set; }
    /// <summary>The account that owns this health data: nobody else in the household sees it.</summary>
    public Guid OwnerUserId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public string Name { get; private set; } = "";
    public string? Notes { get; private set; }
    public decimal Dose { get; private set; }
    public string Unit { get; private set; } = "gelule";
    public string Moment { get; private set; } = "choix";
    /// <summary>Contents of one package, in <see cref="Unit"/>.</summary>
    public decimal PackageContent { get; private set; }
    public decimal? PackagePrice { get; private set; }
    public string? Shop { get; private set; }
    public SupplementPeriod Period { get; private set; } = null!;
    public bool IsStopped { get; private set; }
    public string? ImagePath { get; private set; }
    public string? ImageContentType { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyList<SupplementIntake> Intakes => _intakes;
    public IReadOnlyList<SupplementLink> Links => _links;
    public IEnumerable<SupplementLink> OrderedLinks => _links.OrderBy(l => l.Position);
    public bool HasImage => ImagePath is not null;

    private Supplement() { }

    public static Supplement Create(Guid householdId, Guid ownerUserId, Guid memberProfileId, string name, string? notes, decimal dose,
        string unit, string? moment, decimal packageContent, decimal? packagePrice, string? shop, IEnumerable<(string? Label, string Url)>? links,
        SupplementPeriod period, DateTimeOffset now)
    {
        if (householdId == Guid.Empty || ownerUserId == Guid.Empty || memberProfileId == Guid.Empty)
            throw new ArgumentException("Foyer, compte et profil obligatoires.");
        var supplement = new Supplement
        {
            Id = Guid.NewGuid(), HouseholdId = householdId, OwnerUserId = ownerUserId, MemberProfileId = memberProfileId, CreatedAt = now,
        };
        supplement.Update(name, notes, dose, unit, moment, packageContent, packagePrice, shop, links, period, now);
        return supplement;
    }

    public void Update(string name, string? notes, decimal dose, string unit, string? moment, decimal packageContent, decimal? packagePrice,
        string? shop, IEnumerable<(string? Label, string Url)>? links, SupplementPeriod period, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength)
            throw new ArgumentException($"Nom obligatoire ({MaxNameLength} caractères maximum).");
        var text = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (text?.Length > MaxNotesLength) throw new ArgumentException($"Notes : {MaxNotesLength} caractères maximum.");
        if (dose is <= 0 or > 10000) throw new ArgumentException("Dose par jour : supérieure à 0.");
        var unitCode = unit?.Trim() ?? "";
        if (!Units.ContainsKey(unitCode)) throw new ArgumentException("Unité : gelule, comprime, g, ml, goutte ou dose.");
        var momentCode = string.IsNullOrWhiteSpace(moment) ? "choix" : moment.Trim();
        if (!Moments.ContainsKey(momentCode)) throw new ArgumentException("Moment : matin, midi, soir ou choix.");
        if (packageContent is <= 0 or > 1_000_000) throw new ArgumentException("Contenu du conditionnement : supérieur à 0, dans l'unité de la dose.");
        if (packagePrice is < 0 or > 100_000) throw new ArgumentException("Prix du conditionnement invalide.");
        var shopText = string.IsNullOrWhiteSpace(shop) ? null : shop.Trim();
        if (shopText?.Length > 200) throw new ArgumentException("Magasin : 200 caractères maximum.");
        var cleanLinks = (links ?? []).Select((l, i) => new SupplementLink(i, l.Label, l.Url)).ToList();
        if (cleanLinks.Count > MaxLinks) throw new ArgumentException($"Au plus {MaxLinks} liens d'achat.");
        Name = name.Trim();
        Notes = text;
        Dose = dose;
        Unit = unitCode;
        Moment = momentCode;
        PackageContent = packageContent;
        PackagePrice = packagePrice;
        Shop = shopText;
        Period = period ?? throw new ArgumentException("Période obligatoire.");
        _links.Clear();
        _links.AddRange(cleanLinks);
        UpdatedAt = now;
    }

    /// <summary>
    /// Monthly cost (a month = 30 days): price × dose × 30 / contents; a repeating cure counts only
    /// its share of days in cure. Unknown when the price is not known. Units are never converted.
    /// </summary>
    public decimal? MonthlyCost => PackagePrice is { } price
        ? Math.Round(price * Dose * 30 / PackageContent * Period.CureShare, 2)
        : null;

    public bool TakenOn(DateOnly day) => _intakes.Any(i => i.Day == day);

    /// <summary>Ticks or unticks a day; only for a supplement that is not stopped, never in the future.</summary>
    public void SetTaken(DateOnly day, bool taken, DateOnly today, DateTimeOffset now)
    {
        if (IsStopped) throw new ArgumentException("Ce complément est arrêté.");
        if (day > today) throw new ArgumentException("Une prise ne peut pas être dans le futur.");
        var existing = _intakes.FirstOrDefault(i => i.Day == day);
        if (taken && existing is null) _intakes.Add(new SupplementIntake(day));
        if (!taken && existing is not null) _intakes.Remove(existing);
        UpdatedAt = now;
    }

    /// <summary>Days taken in a row up to today (or yesterday while today is not ticked); days out of cure are skipped.</summary>
    public int Streak(DateOnly today)
    {
        var days = _intakes.Select(i => i.Day).ToHashSet();
        var streak = 0;
        for (var day = today; day >= today.AddDays(-730); day = day.AddDays(-1))
        {
            if (!Period.StatusOn(day).Active) continue;
            if (days.Contains(day)) streak++;
            else if (day != today) break;
        }
        return streak;
    }

    public void Stop(DateTimeOffset now)
    {
        if (IsStopped) throw new ArgumentException("Ce complément est déjà arrêté.");
        IsStopped = true;
        UpdatedAt = now;
    }

    public void Resume(DateTimeOffset now)
    {
        if (!IsStopped) throw new ArgumentException("Ce complément n'est pas arrêté.");
        IsStopped = false;
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

/// <summary>A day the supplement was taken.</summary>
public sealed class SupplementIntake
{
    public DateOnly Day { get; private set; }

    private SupplementIntake() { }

    internal SupplementIntake(DateOnly day) => Day = day;
}

/// <summary>A purchase link: an optional name and an https address.</summary>
public sealed class SupplementLink
{
    public const int MaxUrlLength = 1000;

    public int Position { get; private set; }
    public string Label { get; private set; } = "";
    public string Url { get; private set; } = "";

    private SupplementLink() { }

    internal SupplementLink(int position, string? label, string url)
    {
        var address = url?.Trim() ?? "";
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || address.Length > MaxUrlLength)
            throw new ArgumentException("Chaque lien d'achat est une adresse https.");
        var name = string.IsNullOrWhiteSpace(label) ? uri.Host.Replace("www.", "") : label.Trim();
        if (name.Length > 100) throw new ArgumentException("Nom d'un lien : 100 caractères maximum.");
        Position = position;
        Label = name;
        Url = address;
    }
}

/// <summary>Where a supplement stands on a day.</summary>
/// <param name="DayInCure">1-based day in the current cure; null all year or out of cure.</param>
/// <param name="DaysLeft">Days left in the current cure, today included.</param>
/// <param name="NextCureOn">When paused between two cures: the day the next cure starts.</param>
public sealed record SupplementStatus(bool Active, int? DayInCure, int? CureDays, int? DaysLeft, DateOnly? NextCureOn, bool Ended);

/// <summary>
/// When a supplement is taken (ADR 0018): <c>year</c> from a start day, or a <c>cure</c> of N weeks or
/// months from a start day, repeated after a pause of N weeks or months when a pause is given.
/// A week is 7 days and a month 30 days.
/// </summary>
public sealed record SupplementPeriod
{
    public string Kind { get; private init; } = "year";
    public DateOnly StartOn { get; private init; }
    public int CureLength { get; private init; }
    public string CureUnit { get; private init; } = "month";
    public int PauseLength { get; private init; }
    public string PauseUnit { get; private init; } = "month";

    private SupplementPeriod() { }

    public static SupplementPeriod AllYear(DateOnly startOn) => new() { Kind = "year", StartOn = startOn };

    /// <param name="pauseLength">0: a single cure.</param>
    public static SupplementPeriod Cure(DateOnly startOn, int cureLength, string cureUnit, int pauseLength, string pauseUnit)
    {
        if (cureUnit is not ("week" or "month") || pauseUnit is not ("week" or "month"))
            throw new ArgumentException("Durée en semaines (week) ou en mois (month).");
        if (cureLength is < 1 or > 52) throw new ArgumentException("Durée de la cure : de 1 à 52.");
        if (pauseLength is < 0 or > 52) throw new ArgumentException("Pause : de 0 à 52.");
        return new SupplementPeriod { Kind = "cure", StartOn = startOn, CureLength = cureLength, CureUnit = cureUnit, PauseLength = pauseLength, PauseUnit = pauseUnit };
    }

    public int CureDays => Kind == "cure" ? Days(CureLength, CureUnit) : 0;
    public int PauseDays => Kind == "cure" ? Days(PauseLength, PauseUnit) : 0;

    /// <summary>Share of days in cure over a cycle; 1 all year or for a single cure.</summary>
    public decimal CureShare => Kind == "cure" && PauseDays > 0 ? (decimal)CureDays / (CureDays + PauseDays) : 1;

    public SupplementStatus StatusOn(DateOnly day)
    {
        var offset = day.DayNumber - StartOn.DayNumber;
        if (offset < 0) return new SupplementStatus(false, null, null, null, Kind == "cure" ? StartOn : null, false);
        if (Kind == "year") return new SupplementStatus(true, null, null, null, null, false);
        if (PauseDays == 0)
            return offset < CureDays
                ? new SupplementStatus(true, offset + 1, CureDays, CureDays - offset, null, false)
                : new SupplementStatus(false, null, CureDays, null, null, true);
        var position = offset % (CureDays + PauseDays);
        return position < CureDays
            ? new SupplementStatus(true, position + 1, CureDays, CureDays - position, null, false)
            : new SupplementStatus(false, null, CureDays, null, day.AddDays(CureDays + PauseDays - position), false);
    }

    private static int Days(int length, string unit) => unit == "week" ? length * 7 : length * 30;
}
