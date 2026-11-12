using LifeOS.Domain.Households;

namespace LifeOS.Domain.Tests.Households;

public sealed class UserAccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_normalizes_the_email_and_trims_the_name()
    {
        var account = UserAccount.Create("google-123", "  Brice@Example.COM ", "  Brice  ", Now);

        Assert.NotEqual(Guid.Empty, account.Id);
        Assert.Equal("google-123", account.GoogleSubject);
        Assert.Equal("brice@example.com", account.Email);
        Assert.Equal("Brice", account.DisplayName);
        Assert.Equal(Now, account.CreatedAt);
        Assert.Equal(Now, account.LastSignInAt);
    }

    [Theory]
    [InlineData("", "a@b.c")]
    [InlineData("sub", "")]
    [InlineData("sub", "not-an-email")]
    public void Create_rejects_a_missing_subject_or_an_invalid_email(string subject, string email) =>
        Assert.Throws<ArgumentException>(() => UserAccount.Create(subject, email, null, Now));

    [Fact]
    public void RecordSignIn_refreshes_email_name_and_date_but_keeps_creation()
    {
        var account = UserAccount.Create("sub", "old@example.com", "Old", Now);

        account.RecordSignIn("New@Example.com", " ", Now.AddDays(3));

        Assert.Equal("new@example.com", account.Email);
        Assert.Null(account.DisplayName);
        Assert.Equal(Now, account.CreatedAt);
        Assert.Equal(Now.AddDays(3), account.LastSignInAt);
    }
}
