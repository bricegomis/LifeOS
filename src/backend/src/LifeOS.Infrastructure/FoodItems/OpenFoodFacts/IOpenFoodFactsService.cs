namespace LifeOS.Infrastructure.FoodItems.OpenFoodFacts;

/// <summary>
/// Service contract for querying the Open Food Facts API (Jalon 3).
/// </summary>
public interface IOpenFoodFactsService
{
    /// <summary>
    /// Searches Open Food Facts by product name.
    /// </summary>
    Task<IEnumerable<OpenFoodFactsProductDto>> SearchByNameAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches Open Food Facts by barcode.
    /// </summary>
    Task<OpenFoodFactsProductDto?> SearchByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);
}
