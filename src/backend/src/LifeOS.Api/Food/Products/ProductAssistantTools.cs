using System.ComponentModel;
using LifeOS.Api.Assistant;
using LifeOS.Application.Food.Products;
using ModelContextProtocol.Server;

namespace LifeOS.Api.Food.Products;

/// <summary>
/// Product and price tools for the assistant (ADR 0011): CRUD through <see cref="ProductLibrary"/>,
/// prices through the same use cases as the endpoints.
/// </summary>
[McpServerToolType]
public sealed class ProductAssistantTools
{
    private const string PackageHelp =
        "Un produit est une référence commerciale (marque, contenance) d'un aliment du foyer ; " +
        "sa contenance est obligatoire : packageQuantity et packageUnit (g, kg, ml, L ou pièce).";

    [McpServerTool(Name = "search_products", ReadOnly = true, Title = "Chercher des produits")]
    [Description("Liste les produits du foyer, avec leur dernier prix par magasin (latestPrices) et l'historique (prices). " +
        "Filtre optionnel par aliment et par texte dans le nom ou la marque.")]
    public static async Task<List<ProductDto>> SearchProducts(IHttpContextAccessor accessor, ProductLibrary products,
        [Description("Seulement les produits de cet aliment.")] Guid? foodId = null,
        [Description("Texte cherché dans le nom ou la marque.")] string? query = null,
        bool includeArchived = false,
        [Description("Nombre maximum de résultats.")] int limit = 50,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var found = await products.ListAsync(household.HouseholdId, foodId, query, includeArchived, ct);
        return found.Take(Math.Clamp(limit, 1, 500)).Select(ProductDto.From).ToList();
    }

    [McpServerTool(Name = "create_product", Title = "Créer un produit")]
    [Description("Crée un produit rattaché à un aliment actif du foyer. " + PackageHelp)]
    public static async Task<ProductDto> CreateProduct(IHttpContextAccessor accessor, ProductLibrary products,
        Guid foodId,
        [Description("Nom du produit tel qu'en magasin, par exemple « Riz basmati 1 kg ».")] string name,
        [Description("Contenance du paquet.")] decimal packageQuantity,
        [Description("g, kg, ml, L ou pièce.")] string packageUnit,
        string? brand = null, string? barcode = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = AssistantRequest.Validated(new ProductRequest(foodId, name, packageQuantity, packageUnit, brand, barcode));
        return ProductDto.From(await products.CreateAsync(household.HouseholdId, request, ct));
    }

    [McpServerTool(Name = "update_product", Idempotent = true, Title = "Modifier un produit")]
    [Description("Remplace toute la fiche d'un produit (un champ omis devient vide). Les prix déjà relevés gardent " +
        "la contenance de leur relevé. " + PackageHelp)]
    public static async Task<ProductDto> UpdateProduct(IHttpContextAccessor accessor, ProductLibrary products,
        Guid productId, Guid foodId, string name, decimal packageQuantity,
        [Description("g, kg, ml, L ou pièce.")] string packageUnit,
        string? brand = null, string? barcode = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = AssistantRequest.Validated(new ProductRequest(foodId, name, packageQuantity, packageUnit, brand, barcode));
        return ProductDto.From((await products.UpdateAsync(household.HouseholdId, productId, request, ct)).OrNotFound("Produit"));
    }

    [McpServerTool(Name = "archive_product", Idempotent = true, Title = "Archiver un produit")]
    [Description("Archive un produit : il ne compte plus dans le coût des recettes et n'accepte plus de prix. " +
        "Demandez confirmation à l'utilisateur avant.")]
    public static async Task<ProductDto> ArchiveProduct(IHttpContextAccessor accessor, ProductLibrary products,
        Guid productId, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return ProductDto.From((await products.ArchiveAsync(household.HouseholdId, productId, ct)).OrNotFound("Produit"));
    }

    [McpServerTool(Name = "add_price", Title = "Saisir un prix")]
    [Description("Enregistre le prix payé pour un paquet du produit, dans un magasin du foyer, à une date. " +
        "Le prix fige la contenance actuelle du produit et se compare au kg, au L ou à la pièce. " +
        "Pour un ticket, saisissez seulement les prix confirmés par l'utilisateur.")]
    public static async Task<ProductDto> AddPrice(IHttpContextAccessor accessor, RecordProductPrice useCase,
        Guid productId, Guid storeId,
        [Description("Prix d'un paquet en euros, deux décimales maximum.")] decimal price,
        [Description("Date du relevé ou de l'achat (aaaa-MM-jj).")] DateOnly observedOn,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return ProductDto.From((await useCase.ExecuteAsync(household.HouseholdId, productId, storeId, price, observedOn, ct))
            .OrNotFound("Produit"));
    }

    [McpServerTool(Name = "remove_price", Destructive = true, Title = "Supprimer un prix")]
    [Description("Supprime un prix mal saisi (identifiant de prix donné par search_products).")]
    public static async Task<ProductDto> RemovePrice(IHttpContextAccessor accessor, RemoveProductPrice useCase,
        Guid productId, Guid priceId, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return ProductDto.From((await useCase.ExecuteAsync(household.HouseholdId, productId, priceId, ct)).OrNotFound("Produit ou prix"));
    }
}
