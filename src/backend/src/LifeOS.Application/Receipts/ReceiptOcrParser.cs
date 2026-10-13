using System.Globalization;
using System.Text.RegularExpressions;
using LifeOS.Domain.Receipts;

namespace LifeOS.Application.Receipts;

public sealed record ParsedReceiptOcr(string StoreName, DateTimeOffset? PurchasedAt,
    IReadOnlyList<ReceiptOcrCandidate> Lines);

public static partial class ReceiptOcrParser
{
    private static readonly string[] DateFormats =
    [
        "dd/MM/yyyy HH:mm", "dd/MM/yyyy H:mm", "dd/MM/yyyy",
        "dd-MM-yyyy HH:mm", "dd-MM-yyyy H:mm", "dd-MM-yyyy",
        "dd.MM.yyyy HH:mm", "dd.MM.yyyy H:mm", "dd.MM.yyyy",
        "dd/MM/yy HH:mm", "dd/MM/yy H:mm", "dd/MM/yy",
    ];

    public static ParsedReceiptOcr Parse(string text)
    {
        var lines = (text ?? "").Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .ToArray();

        var storeName = lines.FirstOrDefault(value => !DatePattern().IsMatch(value) && value.Length <= 200) ?? "";
        DateTimeOffset? purchasedAt = null;
        var dateLine = lines.FirstOrDefault(value => DatePattern().IsMatch(value));
        if (dateLine is not null)
        {
            var match = DatePattern().Match(dateLine);
            var rawDate = match.Groups["date"].Value;
            var rawTime = match.Groups["time"].Success ? match.Groups["time"].Value : "";
            var input = string.IsNullOrEmpty(rawTime) ? rawDate : $"{rawDate} {rawTime}";
            if (DateTime.TryParseExact(input, DateFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsed))
                purchasedAt = new DateTimeOffset(DateTime.SpecifyKind(parsed, DateTimeKind.Utc));
        }

        var candidates = new List<ReceiptOcrCandidate>();
        foreach (var source in lines)
        {
            if (DatePattern().IsMatch(source) || IsNonProductLine(source)) continue;
            var amountMatch = AmountPattern().Match(source);
            if (!amountMatch.Success) continue;
            var label = source[..amountMatch.Index].Trim().TrimEnd('-', ':', '*');
            decimal? quantity = null;
            var quantityMatch = QuantityPrefixPattern().Match(label);
            if (quantityMatch.Success)
            {
                if (decimal.TryParse(quantityMatch.Groups["quantity"].Value.Replace(',', '.'),
                        NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsedQuantity)
                    && parsedQuantity is > 0 and <= 1000000)
                {
                    quantity = parsedQuantity;
                    label = quantityMatch.Groups["label"].Value.Trim();
                }
            }

            if (label.Length == 0 || label.Length > 500) continue;
            if (!decimal.TryParse(amountMatch.Groups["amount"].Value.Replace(',', '.'),
                    NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var total)
                || total is < 0 or > 1000000)
                continue;

            candidates.Add(new ReceiptOcrCandidate(source, label, total, quantity, null, null));
        }

        return new ParsedReceiptOcr(storeName, purchasedAt, candidates);
    }

    private static bool IsNonProductLine(string line)
    {
        var normalized = line.ToUpperInvariant();
        return normalized.StartsWith("TOTAL", StringComparison.Ordinal)
            || normalized.StartsWith("SOUS-TOTAL", StringComparison.Ordinal)
            || normalized.StartsWith("SOUS TOTAL", StringComparison.Ordinal)
            || normalized.StartsWith("TVA", StringComparison.Ordinal)
            || normalized.StartsWith("CB ", StringComparison.Ordinal)
            || normalized.StartsWith("CARTE ", StringComparison.Ordinal)
            || normalized.StartsWith("RENDU", StringComparison.Ordinal)
            || normalized.StartsWith("MONNAIE", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"(?<date>\d{1,2}[./-]\d{1,2}[./-]\d{2,4})(?:\s+(?<time>\d{1,2}:\d{2}))?")]
    private static partial Regex DatePattern();

    [GeneratedRegex(@"(?<amount>\d{1,6}[,.]\d{2})\s*(?:€|EUR)?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex AmountPattern();

    [GeneratedRegex(@"^(?<quantity>\d+(?:[,.]\d+)?)\s*[x×*]\s*(?<label>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex QuantityPrefixPattern();
}
