using LifeOS.Domain.Articles;

namespace LifeOS.Application.Articles;

/// <summary>
/// Maps <see cref="GroceryItem"/> aggregates to their API read model.
/// </summary>
internal static class ArticleMapper
{
    public static GroceryItemDto ToDto(GroceryItem article)
    {
        return new GroceryItemDto(
            article.Id,
            article.Name,
            article.Description,
            ToUnitString(article.Unit),
            article.PriceHistory
                .Select(entry => new GroceryPriceEntryDto(entry.Id, entry.StoreId, entry.Price, entry.ObservedAt, entry.CreatedAt))
                .OrderBy(entry => entry.ObservedAt)
                .ToList(),
            article.CreatedAt,
            article.UpdatedAt);
    }

    public static string ToUnitString(GroceryItemUnit unit) => unit switch
    {
        GroceryItemUnit.Kilogram => "kilogram",
        GroceryItemUnit.Liter => "liter",
        GroceryItemUnit.Unit => "unit",
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown grocery item unit."),
    };

    public static GroceryItemUnit ParseUnit(string unit) => unit switch
    {
        "kilogram" => GroceryItemUnit.Kilogram,
        "liter" => GroceryItemUnit.Liter,
        "unit" => GroceryItemUnit.Unit,
        _ => throw new ArgumentException($"Unknown grocery item unit '{unit}'.", nameof(unit)),
    };
}
