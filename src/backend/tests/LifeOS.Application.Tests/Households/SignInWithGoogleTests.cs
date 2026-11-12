using LifeOS.Application.Common.Interfaces;
using LifeOS.Application.Households;
using LifeOS.Application.Tests.Food;
using LifeOS.Domain.Households;

namespace LifeOS.Application.Tests.Households;

public sealed class SignInWithGoogleTests
{
    private sealed class FakeAccounts : IUserAccountRepository
    {
        public readonly List<UserAccount> Accounts = [];
        public int Saves;

        public Task<UserAccount?> FindByGoogleSubjectAsync(string googleSubject, CancellationToken cancellationToken = default) =>
            Task.FromResult(Accounts.SingleOrDefault(a => a.GoogleSubject == googleSubject));
        public Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Accounts.SingleOrDefault(a => a.Id == userId));
        public Task SaveAsync(UserAccount account, bool isNew, CancellationToken cancellationToken = default)
        {
            if (isNew) Accounts.Add(account);
            Saves++;
            return Task.CompletedTask;
        }
    }

    private readonly FakeAccounts _accounts = new();
    private readonly AccountAccessPolicy _policy = new(["brice@example.com", " Other@Example.com "]);

    private SignInWithGoogle SignIn() => new(_accounts, _policy, FixedClock.Default);

    [Fact]
    public async Task First_sign_in_of_an_allowed_verified_address_creates_the_account()
    {
        var id = await SignIn().ExecuteAsync(new GoogleIdentity("g-1", "Brice@Example.com", true, "Brice"));

        var account = Assert.Single(_accounts.Accounts);
        Assert.Equal(account.Id, id);
        Assert.Equal("brice@example.com", account.Email);
    }

    [Fact]
    public async Task Next_sign_ins_reuse_the_account_of_the_same_Google_subject()
    {
        var first = await SignIn().ExecuteAsync(new GoogleIdentity("g-1", "brice@example.com", true, "Brice"));
        var second = await SignIn().ExecuteAsync(new GoogleIdentity("g-1", "brice@example.com", true, "Brice G."));

        Assert.Equal(first, second);
        Assert.Equal("Brice G.", Assert.Single(_accounts.Accounts).DisplayName);
    }

    [Theory]
    [InlineData("stranger@example.com", true)]
    [InlineData("brice@example.com", false)]
    [InlineData(null, true)]
    public async Task Unlisted_or_unverified_addresses_are_refused_without_creating_anything(string? email, bool verified)
    {
        var id = await SignIn().ExecuteAsync(new GoogleIdentity("g-2", email, verified, null));

        Assert.Null(id);
        Assert.Empty(_accounts.Accounts);
        Assert.Equal(0, _accounts.Saves);
    }

    [Fact]
    public async Task An_account_removed_from_the_allowed_list_is_no_longer_active()
    {
        var id = (await SignIn().ExecuteAsync(new GoogleIdentity("g-1", "other@example.com", true, null)))!.Value;

        Assert.NotNull(await new GetActiveAccount(_accounts, _policy).ExecuteAsync(id));
        Assert.Null(await new GetActiveAccount(_accounts, new AccountAccessPolicy(["brice@example.com"])).ExecuteAsync(id));
        Assert.Null(await new GetActiveAccount(_accounts, _policy).ExecuteAsync(Guid.NewGuid()));
    }
}
