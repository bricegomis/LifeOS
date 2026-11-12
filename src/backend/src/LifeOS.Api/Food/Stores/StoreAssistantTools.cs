using System.ComponentModel;
using LifeOS.Api.Assistant;
using ModelContextProtocol.Server;

namespace LifeOS.Api.Food.Stores;

/// <summary>Store tools for the assistant (ADR 0011): same code as the endpoints, through <see cref="StoreLibrary"/>.</summary>
[McpServerToolType]
public sealed class StoreAssistantTools
{
    [McpServerTool(Name = "list_stores", ReadOnly = true, Title = "Magasins")]
    [Description("Liste les magasins du foyer. Un prix et un ticket se rattachent à l'un d'eux.")]
    public static async Task<List<StoreDto>> ListStores(IHttpContextAccessor accessor, StoreLibrary stores, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await stores.ListAsync(household.HouseholdId, ct)).Select(StoreDto.From).ToList();
    }

    [McpServerTool(Name = "create_store", Title = "Créer un magasin")]
    [Description("Crée un magasin du foyer. Vérifiez d'abord avec list_stores qu'il n'existe pas déjà.")]
    public static async Task<StoreDto> CreateStore(IHttpContextAccessor accessor, StoreLibrary stores,
        [Description("Nom, par exemple « Biocoop Nation ».")] string name,
        string? address = null,
        [Description("Magasin bio.")] bool isOrganic = false,
        [Description("Commerce local ou producteur.")] bool isLocal = false,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = AssistantRequest.Validated(new StoreRequest(name, address ?? "", isOrganic, isLocal));
        return StoreDto.From(await stores.CreateAsync(household.HouseholdId, request, ct));
    }

    [McpServerTool(Name = "update_store", Idempotent = true, Title = "Modifier un magasin")]
    [Description("Remplace le nom, l'adresse et les indicateurs bio et local d'un magasin ; un champ omis devient vide ou faux.")]
    public static async Task<StoreDto> UpdateStore(IHttpContextAccessor accessor, StoreLibrary stores, Guid storeId,
        string name, string? address = null, bool isOrganic = false, bool isLocal = false, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = AssistantRequest.Validated(new StoreRequest(name, address ?? "", isOrganic, isLocal));
        return StoreDto.From((await stores.UpdateAsync(household.HouseholdId, storeId, request, ct)).OrNotFound("Magasin"));
    }

    [McpServerTool(Name = "delete_store", Destructive = true, Title = "Supprimer un magasin")]
    [Description("Supprime un magasin. Refusé s'il porte encore des prix ou des tickets. Demandez confirmation à l'utilisateur avant.")]
    public static async Task<string> DeleteStore(IHttpContextAccessor accessor, StoreLibrary stores, Guid storeId,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        if (!await stores.DeleteAsync(household.HouseholdId, storeId, ct)) throw AssistantRequest.NotFound("Magasin");
        return "Magasin supprimé.";
    }
}
