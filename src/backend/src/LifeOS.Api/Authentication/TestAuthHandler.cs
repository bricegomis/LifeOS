using System.Security.Claims;
using System.Text.Encodings.Web;
using LifeOS.Domain.Households;
using LifeOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LifeOS.Api.Authentication;

/// <summary>
/// Test-only authentication handler activated when <c>ASPNETCORE_ENVIRONMENT=Testing</c> (see
/// <see cref="AuthenticationSetup"/>). Reads the user id from the <see cref="SubHeaderName"/>
/// request header instead of validating a real access token, and creates the matching
/// <see cref="UserAccount"/> on first sight, as a Google sign-in would.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string SubHeaderName = "X-Test-Sub";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(SubHeaderName, out var subValues) || string.IsNullOrWhiteSpace(subValues))
            return AuthenticateResult.NoResult();
        if (!Guid.TryParse(subValues.ToString(), out var userId))
            return AuthenticateResult.Fail("Invalid test user id.");

        await EnsureAccountAsync(userId);

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    private async Task EnsureAccountAsync(Guid userId)
    {
        var db = Context.RequestServices.GetRequiredService<LifeOSDbContext>();
        if (await db.UserAccounts.AnyAsync(user => user.Id == userId)) return;
        var now = DateTimeOffset.UtcNow;
        var account = UserAccount.Rehydrate(userId, $"test-{userId:N}", $"{userId:N}@test.lifeos", null, now, now);
        db.UserAccounts.Add(account);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // A concurrent request of the same simulated user created it first.
            db.Entry(account).State = EntityState.Detached;
        }
    }
}
