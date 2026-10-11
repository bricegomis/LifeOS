using LifeOS.Domain.Receipts;

namespace LifeOS.Domain.Tests.Receipts;

public sealed class ReceiptTests
{
    [Theory]
    [InlineData("1e100")]
    [InlineData("-2")]
    [InlineData("\"pas un prix\"")]
    [InlineData("0.1234567")]
    public void Invalid_stated_price_is_preserved_unknown_and_blocks_until_explicit_correction(string value)
    {
        var raw = """{"aliments":[{"libelle":"Orange","prix_achat":3,"quantite":1,"unite":"kg","prix_quantite":VALUE}]}""".Replace("VALUE", value);
        var line = Assert.Single(Receipt.Import(Guid.NewGuid(), raw).Lines);
        Assert.Null(line.StatedUnitPrice);
        Assert.Contains(value, line.RawSource);
        Assert.Contains(line.Diagnostics, d => d.Contains("source invalide"));
        Assert.Throws<ArgumentException>(() => line.Validate(Guid.NewGuid()));
        line.Correct("Orange", 3, 1, "kg", null, false);
        Assert.Empty(line.Diagnostics);
        line.Validate(Guid.NewGuid());
        Assert.Equal("validated", line.Status);
    }
}
