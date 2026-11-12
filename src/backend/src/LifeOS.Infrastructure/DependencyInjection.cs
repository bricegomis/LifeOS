using LifeOS.Application.Common.Interfaces;
using LifeOS.Application.Households;
using LifeOS.Infrastructure.Food.Files;
using LifeOS.Infrastructure.Households;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LifeOS.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the PostgreSQL context, the household core use cases and the file storages.
    /// The connection string is resolved lazily when the <c>DbContext</c> is first created, so
    /// integration tests can inject their own configuration after the host is built.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<LifeOSDbContext>((serviceProvider, options) =>
            options.UseNpgsql(PostgresConnectionStringResolver.Resolve(serviceProvider.GetRequiredService<IConfiguration>())));

        // Household core
        services.AddScoped<IHouseholdRepository, EfHouseholdRepository>();
        services.AddScoped<ResolveHouseholdForUserQuery>();
        services.AddScoped<GetCurrentHouseholdQuery>();

        // Food module files
        services.Configure<FoodImageStorageOptions>(configuration.GetSection("FoodImages"));
        services.AddSingleton<FoodImageStorage>();
        services.Configure<ReceiptPhotoStorageOptions>(configuration.GetSection("Receipts"));
        services.AddSingleton<ReceiptPhotoStorage>();

        return services;
    }
}
