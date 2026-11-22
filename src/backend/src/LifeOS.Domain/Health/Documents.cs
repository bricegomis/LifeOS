using LifeOS.Domain.Common;

namespace LifeOS.Domain.Health;

/// <summary>
/// A health document of the owner (ADR 0018): a prescription, a report, a test result, an assessment
/// (HPI…) or another paper, with a day, a doctor or laboratory, notes and its files (photos or PDF,
/// stored encrypted). Nothing is read from the files by the API.
/// </summary>
public sealed class HealthDocument : Entity
{
    public static readonly IReadOnlyDictionary<string, string> Types = new Dictionary<string, string>
    {
        ["ordonnance"] = "Ordonnance", ["compte_rendu"] = "Compte rendu", ["resultat"] = "Résultat d'analyse", ["bilan"] = "Bilan", ["autre"] = "Autre",
    };

    public const int MaxFiles = 20;
    public const int MaxNotesLength = 4000;

    private readonly List<HealthDocumentFile> _files = [];

    public Guid HouseholdId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public string Type { get; private set; } = "autre";
    public string Title { get; private set; } = "";
    public DateOnly Day { get; private set; }
    public string? Who { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyList<HealthDocumentFile> Files => _files;
    public IEnumerable<HealthDocumentFile> OrderedFiles => _files.OrderBy(f => f.Position);

    private HealthDocument() { }

    public static HealthDocument Create(Guid householdId, Guid ownerUserId, Guid memberProfileId, string type, string title, DateOnly day, string? who,
        string? notes, DateOnly today, DateTimeOffset now)
    {
        if (householdId == Guid.Empty || ownerUserId == Guid.Empty || memberProfileId == Guid.Empty)
            throw new ArgumentException("Foyer, compte et profil obligatoires.");
        var document = new HealthDocument { Id = Guid.NewGuid(), HouseholdId = householdId, OwnerUserId = ownerUserId, MemberProfileId = memberProfileId, CreatedAt = now };
        document.Update(type, title, day, who, notes, today);
        return document;
    }

    public void Update(string type, string title, DateOnly day, string? who, string? notes, DateOnly today)
    {
        var code = type?.Trim() ?? "";
        if (!Types.ContainsKey(code)) throw new ArgumentException("Type : ordonnance, compte_rendu, resultat, bilan ou autre.");
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200) throw new ArgumentException("Titre obligatoire (200 caractères maximum).");
        if (day > today) throw new ArgumentException("Un document ne peut pas être daté dans le futur.");
        var whoText = string.IsNullOrWhiteSpace(who) ? null : who.Trim();
        if (whoText?.Length > 200) throw new ArgumentException("Médecin ou laboratoire : 200 caractères maximum.");
        var text = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (text?.Length > MaxNotesLength) throw new ArgumentException($"Notes : {MaxNotesLength} caractères maximum.");
        Type = code;
        Title = title.Trim();
        Day = day;
        Who = whoText;
        Notes = text;
    }

    public HealthDocumentFile AddFile(string storedName, string contentType, long size, string? originalName)
    {
        if (_files.Count >= MaxFiles) throw new ArgumentException($"Un document compte au plus {MaxFiles} fichiers.");
        var file = new HealthDocumentFile(Guid.NewGuid(), _files.Count == 0 ? 0 : _files.Max(f => f.Position) + 1, storedName, contentType, size, originalName);
        _files.Add(file);
        return file;
    }

    public HealthDocumentFile RemoveFile(Guid fileId)
    {
        var file = _files.SingleOrDefault(f => f.Id == fileId) ?? throw new ArgumentException("Fichier introuvable.");
        _files.Remove(file);
        return file;
    }
}

/// <summary>One file of a document: a photo or a PDF, stored encrypted under a server-generated name.</summary>
public sealed class HealthDocumentFile
{
    public Guid Id { get; private set; }
    public int Position { get; private set; }
    public string StoredName { get; private set; } = "";
    public string ContentType { get; private set; } = "";
    public long Size { get; private set; }
    public string? OriginalName { get; private set; }

    private HealthDocumentFile() { }

    internal HealthDocumentFile(Guid id, int position, string storedName, string contentType, long size, string? originalName)
    {
        if (contentType is not ("image/jpeg" or "image/png" or "image/webp" or "application/pdf")) throw new ArgumentException("Format de fichier invalide.");
        Id = id;
        Position = position;
        StoredName = storedName;
        ContentType = contentType;
        Size = size;
        var name = string.IsNullOrWhiteSpace(originalName) ? null : Path.GetFileName(originalName.Trim());
        OriginalName = name is { Length: > 200 } ? name[..200] : name;
    }
}

/// <summary>
/// A medical appointment (ADR 0018): a day and optional time, a doctor or speciality, a reason, a
/// place, notes, an optional booking link (Doctolib or another site, https) and, afterwards, its
/// report (a document of the same owner). LifeOS never connects to the booking site.
/// </summary>
public sealed class Appointment : Entity
{
    public Guid HouseholdId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public DateOnly Day { get; private set; }
    public TimeOnly? Time { get; private set; }
    public string Who { get; private set; } = "";
    public string? Reason { get; private set; }
    public string? Place { get; private set; }
    public string? Notes { get; private set; }
    public string? BookingUrl { get; private set; }
    public Guid? ReportDocumentId { get; private set; }

    private Appointment() { }

    public static Appointment Create(Guid householdId, Guid ownerUserId, Guid memberProfileId, DateOnly day, TimeOnly? time, string who, string? reason,
        string? place, string? notes, string? bookingUrl)
    {
        if (householdId == Guid.Empty || ownerUserId == Guid.Empty || memberProfileId == Guid.Empty)
            throw new ArgumentException("Foyer, compte et profil obligatoires.");
        var appointment = new Appointment { Id = Guid.NewGuid(), HouseholdId = householdId, OwnerUserId = ownerUserId, MemberProfileId = memberProfileId };
        appointment.Update(day, time, who, reason, place, notes, bookingUrl);
        return appointment;
    }

    public void Update(DateOnly day, TimeOnly? time, string who, string? reason, string? place, string? notes, string? bookingUrl)
    {
        if (string.IsNullOrWhiteSpace(who) || who.Trim().Length > 200) throw new ArgumentException("Médecin ou spécialité obligatoire (200 caractères maximum).");
        string? Clean(string? value, int max, string label)
        {
            var text = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            return text?.Length > max ? throw new ArgumentException($"{label} : {max} caractères maximum.") : text;
        }
        var url = Clean(bookingUrl, 1000, "Lien");
        if (url is not null && (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("Le lien de prise de rendez-vous est une adresse https.");
        Day = day;
        Time = time;
        Who = who.Trim();
        Reason = Clean(reason, 300, "Motif");
        Place = Clean(place, 300, "Lieu");
        Notes = Clean(notes, 2000, "Notes");
        BookingUrl = url;
    }

    public void SetReport(Guid? documentId) => ReportDocumentId = documentId;
}
