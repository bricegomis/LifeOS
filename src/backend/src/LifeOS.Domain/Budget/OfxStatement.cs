using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace LifeOS.Domain.Budget;

/// <summary>One transaction of a bank statement: bank id (FITID), purchase or debit day, label and signed amount.</summary>
public sealed record StatementTransaction(string BankId, DateOnly Date, DateOnly PostedOn, string Label, decimal Amount);

/// <summary>
/// A bank statement read from an OFX file (ADR 0020). Fixed on a real BoursoBank export (OFX 2.2, XML) and tolerant
/// of OFX 1 (SGML, no closing tags): the account (ACCTID), the balance and, per transaction, DTPOSTED, TRNAMT, FITID
/// and NAME (plus MEMO when it adds something). DTUSER is not reliable and is not read. A card label starting with
/// the purchase day ("CARTE 29/10/26 …", "AVOIR 23/09/26 …") gives the operation's day.
/// </summary>
public sealed partial record OfxStatement(string Account, decimal? Balance, DateOnly? BalanceOn, DateOnly? From, DateOnly? To,
    IReadOnlyList<StatementTransaction> Transactions)
{
    public const int MaxBytes = 5 * 1024 * 1024;

    public static OfxStatement Parse(byte[] content)
    {
        if (content.Length == 0) throw new ArgumentException("Fichier vide.");
        if (content.Length > MaxBytes) throw new ArgumentException("Relevé trop gros (5 Mo maximum).");
        return Parse(Decode(content));
    }

    public static OfxStatement Parse(string text)
    {
        if (!text.Contains("<OFX>", StringComparison.OrdinalIgnoreCase) || !text.Contains("<STMTTRN>", StringComparison.OrdinalIgnoreCase)
            && !text.Contains("<BANKTRANLIST>", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Ce fichier n'est pas un relevé OFX.");
        var account = Tag(text, "ACCTID") ?? throw new ArgumentException("Relevé OFX sans numéro de compte (ACCTID).");
        var transactions = new List<StatementTransaction>();
        foreach (Match block in TransactionBlock().Matches(text))
        {
            var t = block.Groups[1].Value;
            var bankId = Tag(t, "FITID") ?? throw new ArgumentException("Opération sans identifiant bancaire (FITID).");
            var posted = Day(Tag(t, "DTPOSTED")) ?? throw new ArgumentException($"Opération {bankId} sans date (DTPOSTED).");
            var amount = Amount(Tag(t, "TRNAMT")) ?? throw new ArgumentException($"Opération {bankId} sans montant (TRNAMT).");
            var name = Tag(t, "NAME");
            var memo = Tag(t, "MEMO");
            var label = name is null ? memo : memo is null || name.Contains(memo, StringComparison.OrdinalIgnoreCase) ? name : name + " " + memo;
            if (string.IsNullOrWhiteSpace(label)) label = Tag(t, "TRNTYPE") ?? "Opération";
            transactions.Add(new StatementTransaction(bankId, PurchaseDay(label, posted), posted, label, amount));
        }
        var balanceBlock = BlockOf(text, "LEDGERBAL") ?? BlockOf(text, "AVAILBAL");
        return new OfxStatement(account, balanceBlock is null ? null : Amount(Tag(balanceBlock, "BALAMT")), balanceBlock is null ? null : Day(Tag(balanceBlock, "DTASOF")),
            Day(Tag(text, "DTSTART")), Day(Tag(text, "DTEND")), transactions);
    }

    /// <summary>The purchase day written at the start of a card label, when it is plausible (up to 31 days before the debit).</summary>
    public static DateOnly PurchaseDay(string label, DateOnly posted)
    {
        var m = CardDay().Match(label);
        if (!m.Success) return posted;
        if (!DateOnly.TryParseExact(m.Groups[2].Value, "dd/MM/yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)) return posted;
        return day <= posted && posted.DayNumber - day.DayNumber <= 31 ? day : posted;
    }

    private static string Decode(byte[] content)
    {
        var utf8 = new UTF8Encoding(false, true);
        try { return utf8.GetString(content); }
        catch (DecoderFallbackException) { return Encoding.Latin1.GetString(content); }
    }

    private static string? BlockOf(string text, string tag)
    {
        var start = text.IndexOf("<" + tag + ">", StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;
        var end = text.IndexOf("</" + tag + ">", start, StringComparison.OrdinalIgnoreCase);
        return end < 0 ? text[start..Math.Min(text.Length, start + 300)] : text[start..end];
    }

    private static string? Tag(string text, string tag)
    {
        var m = Regex.Match(text, "<" + tag + @">\s*([^<\r\n]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (!m.Success) return null;
        var value = WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
        return value.Length == 0 ? null : value;
    }

    private static DateOnly? Day(string? value) =>
        value is { Length: >= 8 } && DateOnly.TryParseExact(value[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static decimal? Amount(string? value) =>
        value is not null && decimal.TryParse(value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var a) ? a : null;

    [GeneratedRegex(@"<STMTTRN>(.*?)(?=</STMTTRN>|<STMTTRN>|</BANKTRANLIST>)", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant, 2000)]
    private static partial Regex TransactionBlock();

    [GeneratedRegex(@"^\s*(CARTE|AVOIR)\s+(\d{2}/\d{2}/\d{2})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CardDay();
}
