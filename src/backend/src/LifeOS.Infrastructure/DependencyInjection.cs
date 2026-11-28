using LifeOS.Infrastructure.Files;
using LifeOS.Application.Common.Interfaces;
using LifeOS.Application.Food.Costs;
using LifeOS.Application.Food.Products;
using LifeOS.Application.Food.Receipts;
using LifeOS.Application.Households;
using LifeOS.Application.Sports.Contracts;
using LifeOS.Application.Tasks.Contracts;
using LifeOS.Application.Food.Contracts;
using LifeOS.Application.Health.Contracts;
using LifeOS.Infrastructure.Food;
using LifeOS.Infrastructure.Food.Costs;
using LifeOS.Infrastructure.Food.Files;
using LifeOS.Infrastructure.Food.Products;
using LifeOS.Infrastructure.Food.Receipts;
using LifeOS.Infrastructure.Households;
using LifeOS.Infrastructure.Persistence;
using LifeOS.Infrastructure.Sports;
using LifeOS.Infrastructure.Health;
using LifeOS.Infrastructure.Tasks;
using LifeOS.Infrastructure.Reading;
using LifeOS.Application.Reading.Contracts;
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

        // Accounts (ADR 0011). The OpenIddict stores use the same context (OpenIddictModel).
        services.AddScoped<IUserAccountRepository, EfUserAccountRepository>();
        services.AddScoped<SignInWithGoogle>();
        services.AddScoped<GetActiveAccount>();

        // Household core
        services.AddScoped<IHouseholdRepository, EfHouseholdRepository>();
        services.AddScoped<ResolveHouseholdForUserQuery>();
        services.AddScoped<GetCurrentHouseholdQuery>();

        services.AddSingleton(TimeProvider.System);

        // Food module: use cases with rules and the data they need
        services.AddScoped<IProductPriceData, EfProductPriceData>();
        services.AddScoped<RecordProductPrice>();
        services.AddScoped<RemoveProductPrice>();
        services.AddScoped<IRecipeCostData, EfRecipeCostData>();
        services.AddScoped<RecipeCosts>();
        services.AddScoped<IReceiptData, EfReceiptData>();
        services.AddScoped<ReceiptUseCases>();
        services.AddSingleton<IReceiptPhotoFiles>(sp => sp.GetRequiredService<ReceiptPhotoStorage>());

        // Sport module: published read contract (ADR 0013)
        services.AddScoped<ISportReadData, EfSportReadData>();
        services.AddScoped<SportReadContract>();

        // Tasks module: published read contract (ADR 0017)
        services.AddScoped<ITaskReadData, EfTaskReadData>();
        services.AddScoped<TaskReadContract>();

        // Food module: published read contract (ADR 0010)
        services.AddScoped<IFoodReadData, EfFoodReadData>();
        services.AddScoped<FoodReadContract>();

        // Health module: published read contract, sleep and recovery only (ADR 0018, ADR 0019)
        services.AddScoped<IHealthReadData, EfHealthReadData>();
        services.AddScoped<HealthReadContract>();

        // Reading module: published read contract (ADR 0022)
        services.AddScoped<IReadingReadData, EfReadingReadData>();
        services.AddScoped<ReadingReadContract>();

        // Food module files
        services.Configure<FoodImageStorageOptions>(configuration.GetSection("FoodImages"));
        services.AddSingleton<FoodImageStorage>();
        services.Configure<SportImageStorageOptions>(configuration.GetSection("SportImages"));
        services.AddSingleton<SportImageStorage>();
        services.Configure<TaskImageStorageOptions>(configuration.GetSection("TaskImages"));
        services.AddSingleton<TaskImageStorage>();
        services.Configure<BookImageStorageOptions>(configuration.GetSection("BookImages"));
        services.AddSingleton<BookImageStorage>();
        services.Configure<HealthFileStorageOptions>(configuration.GetSection("HealthFiles"));
        services.AddSingleton<HealthFileStorage>();
        services.Configure<ReceiptPhotoStorageOptions>(configuration.GetSection("Receipts"));
        services.AddSingleton<ReceiptPhotoStorage>();
        // Images found by the assistant (ADR 0011): https only, public addresses only.
        services.AddSingleton<IRemoteImageSource, RemoteImageDownloader>();

        return services;
    }
}
