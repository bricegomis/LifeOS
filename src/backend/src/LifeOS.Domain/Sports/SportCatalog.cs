namespace LifeOS.Domain.Sports;

/// <summary>
/// Fixed list of sports (ADR 0013). Codes are stored and exchanged; screens and the assistant
/// show the French labels. Adding a sport needs a migration (check constraints).
/// </summary>
public static class SportCatalog
{
    public static readonly IReadOnlyDictionary<string, string> Sports = new Dictionary<string, string>
    {
        ["running"] = "Course",
        ["walking"] = "Marche",
        ["hiking"] = "Randonnée",
        ["cycling"] = "Vélo",
        ["swimming"] = "Natation",
        ["strength"] = "Musculation",
        ["crossfit"] = "Crossfit",
        ["bodyweight"] = "Renforcement",
        ["mobility"] = "Mobilité",
        ["team_sport"] = "Sport collectif",
        ["racket"] = "Sport de raquette",
        ["other"] = "Autre",
    };

    /// <summary>Sports where a distance makes sense; the others never carry one.</summary>
    public static readonly IReadOnlySet<string> WithDistance = new HashSet<string> { "running", "walking", "hiking", "cycling", "swimming" };

    public static bool HasDistance(string sport) => WithDistance.Contains(sport);

    /// <summary>"running (Course), walking (Marche), …" for error messages and tool descriptions.</summary>
    public static string Describe() => string.Join(", ", Sports.Select(entry => $"{entry.Key} ({entry.Value})"));

    public static string Require(string? sport)
    {
        var code = sport?.Trim() ?? "";
        if (!Sports.ContainsKey(code)) throw new ArgumentException($"Sport inconnu. Valeurs possibles : {Describe()}.");
        return code;
    }

    public static decimal? CheckDistance(string sport, decimal? distanceKm)
    {
        if (distanceKm is null) return null;
        if (!HasDistance(sport)) throw new ArgumentException("Pas de distance pour ce sport.");
        if (distanceKm < 0 || distanceKm > 1000) throw new ArgumentException("Distance : 0 à 1000 km.");
        return distanceKm;
    }

    public static int CheckDuration(int minutes) =>
        minutes is > 0 and <= 1440 ? minutes : throw new ArgumentException("Durée : 1 à 1440 minutes.");

    public static int? CheckCalories(int? calories) =>
        calories is null or (>= 0 and <= 20000) ? calories : throw new ArgumentException("Calories : 0 à 20000 kcal.");
}
