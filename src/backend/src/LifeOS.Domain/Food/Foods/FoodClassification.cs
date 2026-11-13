namespace LifeOS.Domain.Food.Foods;

/// <summary>
/// Fixed lists used to classify a food (ADR 0012). Codes are stored and exchanged; the
/// frontend and the assistant tools show the French labels.
/// </summary>
public static class FoodClassification
{
    public const int MaxTags = 20;
    public const int MaxTagLength = 50;

    /// <summary>What the food is. Exactly one, required.</summary>
    public static readonly IReadOnlyDictionary<string, string> Families = new Dictionary<string, string>
    {
        ["vegetable"] = "Légume",
        ["fruit"] = "Fruit",
        ["grain"] = "Céréale et féculent",
        ["legume"] = "Légumineuse",
        ["meat"] = "Viande",
        ["fish"] = "Poisson et fruits de mer",
        ["egg"] = "Œuf",
        ["dairy"] = "Produit laitier",
        ["fat"] = "Matière grasse",
        ["nut_seed"] = "Fruit à coque et graine",
        ["herb_spice"] = "Herbe et épice",
        ["condiment"] = "Condiment et sauce",
        ["sweet"] = "Produit sucré",
        ["drink"] = "Boisson",
        ["other"] = "Autre",
    };

    /// <summary>What the food brings to a plate. Zero to several.</summary>
    public static readonly IReadOnlyDictionary<string, string> Roles = new Dictionary<string, string>
    {
        ["protein"] = "Protéine",
        ["starch"] = "Féculent",
        ["vegetable"] = "Légume",
        ["fat"] = "Matière grasse",
        ["seasoning"] = "Assaisonnement",
        ["sweet"] = "Sucré",
    };

    /// <summary>How the food is kept. Optional.</summary>
    public static readonly IReadOnlyDictionary<string, string> Conservations = new Dictionary<string, string>
    {
        ["fresh"] = "Frais",
        ["frozen"] = "Surgelé",
        ["canned"] = "Conserve",
        ["dry"] = "Sec",
    };

    /// <summary>"vegetable (Légume), fruit (Fruit), …" for error messages and tool descriptions.</summary>
    public static string Describe(IReadOnlyDictionary<string, string> list) =>
        string.Join(", ", list.Select(entry => $"{entry.Key} ({entry.Value})"));
}
