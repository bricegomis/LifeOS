using LifeOS.Domain.Common;

namespace LifeOS.Domain.Reading;

/// <summary>
/// A series of books of the household (ADR 0022): a name and, when known, its number of tomes. Its books carry
/// their tome number; the whole numbers 1..TotalTomes without a book are the missing tomes, computed on read.
/// </summary>
public sealed class BookSeries : Entity
{
    public const int MaxNameLength = 200;
    public const int MaxTomes = 999;

    public Guid HouseholdId { get; private set; }
    public string Name { get; private set; } = "";
    public int? TotalTomes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private BookSeries() { }

    public static BookSeries Create(Guid householdId, string name, int? totalTomes, DateTimeOffset now)
    {
        if (householdId == Guid.Empty) throw new ArgumentException("Foyer obligatoire.");
        var series = new BookSeries { Id = Guid.NewGuid(), HouseholdId = householdId, CreatedAt = now };
        series.Update(name, totalTomes, now);
        return series;
    }

    public void Update(string name, int? totalTomes, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength)
            throw new ArgumentException($"Nom de la série obligatoire ({MaxNameLength} caractères maximum).");
        if (totalTomes is < 1 or > MaxTomes) throw new ArgumentException($"Nombre de tomes : de 1 à {MaxTomes}.");
        Name = name.Trim();
        TotalTomes = totalTomes;
        UpdatedAt = now;
    }

    /// <summary>Whole tome numbers 1..TotalTomes that no book of the series holds; none when the total is unknown.</summary>
    public IReadOnlyList<int> MissingTomes(IEnumerable<decimal?> tomesHeld)
    {
        if (TotalTomes is not { } total) return [];
        var held = tomesHeld.Where(t => t is not null).Select(t => t!.Value).ToHashSet();
        return Enumerable.Range(1, total).Where(n => !held.Contains(n)).ToList();
    }
}
