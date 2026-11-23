using System.Text;
using LifeOS.Domain.Budget;

namespace LifeOS.Domain.Tests.Budget;

public sealed class OfxStatementTests
{
    /// <summary>Shape of a real BoursoBank export (OFX 2.2, XML on one line), with made-up values.</summary>
    public const string Bourso = """
        <?xml version="1.0" encoding="utf-8"?>
        <?OFX OFXHEADER="200" VERSION="220" SECURITY="NONE" OLDFILEUID="NONE" NEWFILEUID="NONE" ?>

        <OFX><BANKMSGSRSV1><STMTTRNRS><STATUS><CODE>0</CODE><SEVERITY>INFO</SEVERITY></STATUS><STMTRS><CURDEF>EUR</CURDEF><BANKACCTFROM><BANKID>abc</BANKID><ACCTID>00012345678</ACCTID><ACCTTYPE>CHECKINGS</ACCTTYPE></BANKACCTFROM><AVAILBAL><DTASOF>20261030000000</DTASOF><BALAMT>-140.9</BALAMT></AVAILBAL><BANKTRANLIST><STMTTRN><DTPOSTED>20261030</DTPOSTED><TRNTYPE>DEBIT</TRNTYPE><TRNAMT>-37.00</TRNAMT><FITID>f1</FITID><NAME>CARTE 29/10/26 Le Passionne CB*1234</NAME><DTUSER>89746165</DTUSER></STMTTRN><STMTTRN><DTPOSTED>20261030</DTPOSTED><TRNTYPE>CREDIT</TRNTYPE><TRNAMT>2929.36</TRNAMT><FITID>f2</FITID><NAME>VIR SEPA EMPLOYEUR</NAME><DTUSER>89746165</DTUSER></STMTTRN><STMTTRN><DTPOSTED>20261002</DTPOSTED><TRNTYPE>CREDIT</TRNTYPE><TRNAMT>0.94</TRNAMT><FITID>f3</FITID><NAME>AVOIR 23/09/26 Decathlon France CB*1234</NAME><DTUSER>89746165</DTUSER></STMTTRN><STMTTRN><DTPOSTED>20261005</DTPOSTED><TRNTYPE>DEBIT</TRNTYPE><TRNAMT>-6.99</TRNAMT><FITID>f4</FITID><NAME>PRLV SEPA AMAZON EU SARL, SUCCURSALE FRA &amp; CIE</NAME><DTUSER>89746165</DTUSER></STMTTRN></BANKTRANLIST></STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>
        """;

    [Fact]
    public void A_BoursoBank_export_is_read_with_its_account_balance_and_transactions()
    {
        var statement = OfxStatement.Parse(Encoding.UTF8.GetBytes(Bourso));
        Assert.Equal("00012345678", statement.Account);
        Assert.Equal((-140.9m, new DateOnly(2026, 10, 30)), (statement.Balance!.Value, statement.BalanceOn!.Value));
        Assert.Equal(4, statement.Transactions.Count);
        var card = statement.Transactions[0];
        Assert.Equal(("f1", -37m, new DateOnly(2026, 10, 29), new DateOnly(2026, 10, 30)), (card.BankId, card.Amount, card.Date, card.PostedOn));
        Assert.Equal(new DateOnly(2026, 10, 30), statement.Transactions[1].Date);
        Assert.Equal((new DateOnly(2026, 9, 23), 0.94m), (statement.Transactions[2].Date, statement.Transactions[2].Amount));
        Assert.Equal("PRLV SEPA AMAZON EU SARL, SUCCURSALE FRA & CIE", statement.Transactions[3].Label);
    }

    [Fact]
    public void An_sgml_ofx_without_closing_tags_is_read_too()
    {
        var sgml = "OFXHEADER:100\nDATA:OFXSGML\n\n<OFX>\n<BANKACCTFROM>\n<ACCTID>FR76 1\n</BANKACCTFROM>\n<BANKTRANLIST>\n<STMTTRN>\n<TRNTYPE>DEBIT\n<DTPOSTED>20261105120000\n" +
            "<TRNAMT>-12,50\n<FITID>A1\n<NAME>PRLV SEPA FREE\n<MEMO>Forfait novembre\n</STMTTRN>\n<STMTTRN>\n<TRNTYPE>DEBIT\n<DTPOSTED>20261106\n<TRNAMT>-3\n<FITID>A2\n<NAME>CARTE 31/12/25 X\n</STMTTRN>\n</BANKTRANLIST>\n</OFX>";
        var statement = OfxStatement.Parse(sgml);
        Assert.Equal("FR76 1", statement.Account);
        Assert.Equal(("PRLV SEPA FREE Forfait novembre", -12.5m, new DateOnly(2026, 11, 5)), (statement.Transactions[0].Label, statement.Transactions[0].Amount, statement.Transactions[0].Date));
        // A card day too far from the debit is not trusted.
        Assert.Equal(new DateOnly(2026, 11, 6), statement.Transactions[1].Date);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Date;Libellé;Montant\n2026-10-01;X;-3")]
    [InlineData("<OFX><BANKTRANLIST><STMTTRN><FITID>1</FITID></STMTTRN></BANKTRANLIST></OFX>")]
    public void Anything_else_is_refused(string text) =>
        Assert.Throws<ArgumentException>(() => OfxStatement.Parse(Encoding.UTF8.GetBytes(text)));
}
