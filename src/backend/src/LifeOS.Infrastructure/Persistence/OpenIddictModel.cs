using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace LifeOS.Infrastructure.Persistence;

/// <summary>
/// Maps the OpenIddict entities (OAuth clients, grants, tokens; ADR 0011) to the <c>auth</c>
/// schema, so <c>public</c> keeps only the migrations history.
/// </summary>
internal static class OpenIddictModel
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.UseOpenIddict<Guid>();
        modelBuilder.Entity<OpenIddictEntityFrameworkCoreApplication<Guid>>().ToTable("applications", "auth");
        modelBuilder.Entity<OpenIddictEntityFrameworkCoreAuthorization<Guid>>().ToTable("authorizations", "auth");
        modelBuilder.Entity<OpenIddictEntityFrameworkCoreScope<Guid>>().ToTable("scopes", "auth");
        modelBuilder.Entity<OpenIddictEntityFrameworkCoreToken<Guid>>().ToTable("tokens", "auth");
    }
}
