using LifeOS.Application.Articles;
using LifeOS.Application.Common.Interfaces;
using LifeOS.Application.Households;
using LifeOS.Application.Library;
using LifeOS.Application.Planning;
using LifeOS.Application.Stores;
using LifeOS.Application.WeekContexts;
using LifeOS.Infrastructure.Articles;
using LifeOS.Infrastructure.Households;
using LifeOS.Infrastructure.Library;
using LifeOS.Infrastructure.Persistence;
using LifeOS.Infrastructure.Planning;
using LifeOS.Infrastructure.Stores;
using LifeOS.Infrastructure.WeekContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LifeOS.Infrastructure;

/// <summary>
/// Wires the Infrastructure layer's implementations behind the Application layer's ports.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the EF Core / PostgreSQL-backed domains (households, stores, articles) and the
    /// remaining in-memory bounded contexts still awaiting migration. The connection string is
    /// resolved lazily from <see cref="IConfiguration"/> when the <c>DbContext</c> is first
    /// created (not eagerly at startup), so hosts such as integration tests can inject their own
    /// configuration after <c>WebApplicationBuilder</c> construction but before first use.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<LifeOSDbContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            options.UseNpgsql(PostgresConnectionStringResolver.Resolve(configuration));
        });

        services.AddScoped<IHouseholdRepository, EfHouseholdRepository>();
        services.AddScoped<ResolveHouseholdForUserQuery>();

        services.AddScoped<IStoreRepository, EfStoreRepository>();
        services.AddScoped<GetStoresQuery>();

        services.AddScoped<IArticleRepository, EfArticleRepository>();
        services.AddScoped<GetArticlesQuery>();
        services.AddScoped<CreateArticleCommand>();
        services.AddScoped<UpdateArticleCommand>();
        services.AddScoped<DeleteArticleCommand>();
        services.AddScoped<AddPriceEntryCommand>();
        services.AddScoped<DeletePriceEntryCommand>();

        services.AddSingleton<IMealComponentRepository, InMemoryMealComponentRepository>();
        services.AddSingleton<ICompositeDishRepository, InMemoryCompositeDishRepository>();
        services.AddSingleton<IActivityRepository, InMemoryActivityRepository>();
        services.AddScoped<GetMealComponentsQuery>();
        services.AddScoped<GetCompositeDishesQuery>();
        services.AddScoped<GetActivitiesQuery>();

        services.AddSingleton<IPlanningRuleRepository, InMemoryPlanningRuleRepository>();
        services.AddSingleton<IFrequencyRuleRepository, InMemoryFrequencyRuleRepository>();
        services.AddScoped<GetPlanningRulesQuery>();
        services.AddScoped<CreatePlanningRuleCommand>();
        services.AddScoped<UpdatePlanningRuleCommand>();
        services.AddScoped<DeletePlanningRuleCommand>();
        services.AddScoped<GetFrequencyRulesQuery>();
        services.AddScoped<CreateFrequencyRuleCommand>();
        services.AddScoped<UpdateFrequencyRuleCommand>();
        services.AddScoped<DeleteFrequencyRuleCommand>();

        services.AddSingleton<IWeekContextRepository, InMemoryWeekContextRepository>();
        services.AddScoped<GetWeekContextQuery>();
        services.AddScoped<SaveWeekContextCommand>();

        return services;
    }
}
