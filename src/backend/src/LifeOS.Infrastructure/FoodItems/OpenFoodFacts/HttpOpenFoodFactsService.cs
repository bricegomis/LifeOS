using System.Text.Json;
using System.Text.Json.Serialization;

namespace LifeOS.Infrastructure.FoodItems.OpenFoodFacts;

/// <summary>
/// Implementation of Open Food Facts API queries (Jalon 3).
/// Uses the public OFF API; results are cached locally in the database.
/// </summary>
public sealed class HttpOpenFoodFactsService(HttpClient httpClient) : IOpenFoodFactsService
{
    private const string BaseUrl = "https://world.openfoodfacts.net/api/v0";

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<IEnumerable<OpenFoodFactsProductDto>> SearchByNameAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        try
        {
            var url = $"{BaseUrl}/cgi/search.pl?search_terms={Uri.EscapeDataString(query)}&search_simple=1&action=process&json=1&page_size=10";
            var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var searchResponse = JsonSerializer.Deserialize<OpenFoodFactsSearchResponse>(content, _jsonOptions);

            return searchResponse?.Products ?? [];
        }
        catch (Exception ex)
        {
            // Log and return empty results on failure
            System.Diagnostics.Debug.WriteLine($"Open Food Facts search by name failed: {ex.Message}");
            return [];
        }
    }

    public async Task<OpenFoodFactsProductDto?> SearchByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return null;

        try
        {
            var url = $"{BaseUrl}/product/{barcode}.json";
            var response = await httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var json = JsonDocument.Parse(content);
            var product = json.RootElement.GetProperty("product");

            return JsonSerializer.Deserialize<OpenFoodFactsProductDto>(product.GetRawText(), _jsonOptions);
        }
        catch (Exception ex)
        {
            // Log and return null on failure
            System.Diagnostics.Debug.WriteLine($"Open Food Facts search by barcode failed: {ex.Message}");
            return null;
        }
    }
}
