using LifeOS.Application.Receipts;

namespace LifeOS.Api.IntegrationTests;

public sealed class ReceiptOcrParserTests
{
    [Fact]
    public void Parse_keeps_uncertain_values_as_candidates_and_extracts_french_date_quantity_and_total()
    {
        var result = ReceiptOcrParser.Parse("""
            MARCHÉ DU CENTRE
            13/10/2026 18:42
            2 x YAOURT NATURE 3,50
            TOMATES 2.40 €
            TOTAL 5,90
            """);

        Assert.Equal("MARCHÉ DU CENTRE", result.StoreName);
        Assert.Equal(new DateTimeOffset(2026, 10, 13, 18, 42, 0, TimeSpan.Zero), result.PurchasedAt);
        Assert.Equal(2, result.Lines.Count);
        Assert.Equal("YAOURT NATURE", result.Lines[0].Label);
        Assert.Equal(2m, result.Lines[0].Quantity);
        Assert.Equal(3.50m, result.Lines[0].Total);
        Assert.Null(result.Lines[0].Unit);
        Assert.Null(result.Lines[1].Quantity);
        Assert.Equal(2.40m, result.Lines[1].Total);
    }

    [Fact]
    public void Parse_keeps_manual_review_possible_when_date_and_product_rows_are_missing()
    {
        var result = ReceiptOcrParser.Parse("MAGASIN\nTicket illisible");

        Assert.Equal("MAGASIN", result.StoreName);
        Assert.Null(result.PurchasedAt);
        Assert.Empty(result.Lines);
    }
}
