namespace LifeOS.Domain.Common;

public static class QuantityConversion
{
    public static decimal? Convert(decimal quantity, string from, string to)
    {
        var source = Normalize(from);
        var target = Normalize(to);
        if (source == target && source.Length > 0) return quantity;
        var sourceScale = Scale(source);
        var targetScale = Scale(target);
        return sourceScale is { } s && targetScale is { } t && s.Dimension == t.Dimension
            ? quantity * s.Amount / t.Amount : null;
    }

    private static string Normalize(string unit) => unit.Trim().ToLowerInvariant().Replace(" ", "")
        .Replace("pièce", "piece").Replace("pièces", "piece");

    private static (string Dimension, decimal Amount)? Scale(string unit) => unit switch
    {
        "g" => ("mass", 1), "100g" => ("mass", 100), "kg" or "kilogram" => ("mass", 1000),
        "ml" => ("volume", 1), "100ml" => ("volume", 100), "l" or "liter" => ("volume", 1000),
        "piece" or "1piece" or "unit" => ("count", 1),
        _ => null,
    };
}
