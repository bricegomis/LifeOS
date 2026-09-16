using LifeOS.Application.Articles;
using LifeOS.Application.ComposedMeals;
using LifeOS.Application.Common.Interfaces;
using LifeOS.Application.Households;
using LifeOS.Application.Library;
using LifeOS.Application.Planning;
using LifeOS.Application.Recipes;
using LifeOS.Application.Stock;
using LifeOS.Application.Stores;
using LifeOS.Application.WeekContexts;
using LifeOS.Application.WeekPlanning;
using LifeOS.Domain.FoodItems;
using LifeOS.Infrastructure.Articles;
using LifeOS.Infrastructure.ComposedMeals;
using LifeOS.Infrastructure.FoodItems;
using LifeOS.Infrastructure.FoodItems.OpenFoodFacts;
using LifeOS.Infrastructure.Households;
using LifeOS.Infrastructure.Library;
using LifeOS.Infrastructure.Persistence;
using LifeOS.Infrastructure.Planning;
using LifeOS.Infrastructure.Recipes;
using LifeOS.Infrastructure.Stock;
using LifeOS.Infrastructure.Stores;
using LifeOS.Infrastructure.WeekContexts;
using LifeOS.Infrastructure.WeekPlanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

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

        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        services.AddScoped<IHouseholdRepository, EfHouseholdRepository>();
        services.AddScoped<ResolveHouseholdForUserQuery>();
        services.AddScoped<GetCurrentHouseholdQuery>();

        services.AddScoped<IStoreRepository, EfStoreRepository>();
        services.AddScoped<GetStoresQuery>();
        services.AddScoped<GetStoreQuery>();
        services.AddScoped<CreateStoreCommand>();
        services.AddScoped<UpdateStoreCommand>();
        services.AddScoped<DeleteStoreCommand>();

        services.AddScoped<IArticleRepository, EfArticleRepository>();
        services.AddScoped<GetArticlesQuery>();
        services.AddScoped<CreateArticleCommand>();
        services.AddScoped<UpdateArticleCommand>();
        services.AddScoped<DeleteArticleCommand>();
        services.AddScoped<AddPriceEntryCommand>();
        services.AddScoped<DeletePriceEntryCommand>();

        // Jalon 6: Stock and Shopping List
        services.AddScoped<IStockItemRepository, EfStockItemRepository>();
        services.AddScoped<IShoppingListItemRepository, EfShoppingListItemRepository>();
        services.AddScoped<GetStockItemsQuery>();
        services.AddScoped<CreateStockItemCommand>();
        services.AddScoped<UpdateStockItemCommand>();
        services.AddScoped<DeleteStockItemCommand>();
        services.AddScoped<GenerateShoppingListCommand>();
        services.AddScoped<GetShoppingListItemsQuery>();
        services.AddScoped<UpdateShoppingListItemCheckedCommand>();

        // Jalon 2: Recipes and Meals
        services.AddScoped<IRecipeRepository, EfRecipeRepository>();
        services.AddScoped<IComposedMealRepository, EfComposedMealRepository>();
        services.AddScoped<IWeekRepository, EfWeekRepository>();
        services.AddScoped<IDayPlanRepository, EfDayPlanRepository>();
        services.AddScoped<IPlannedMealRepository, EfPlannedMealRepository>();
        services.AddScoped<IWeekScenarioRepository, EfWeekScenarioRepository>();
        services.AddScoped<IScenarioEngine, DeterministicScenarioEngine>();

        services.AddSingleton<IMealComponentRepository, InMemoryMealComponentRepository>();
        services.AddSingleton<ICompositeDishRepository, InMemoryCompositeDishRepository>();
        services.AddSingleton<IActivityRepository, InMemoryActivityRepository>();
        services.AddScoped<GetMealComponentsQuery>();
        services.AddScoped<GetCompositeDishesQuery>();
        services.AddScoped<GetActivitiesQuery>();

        services.AddScoped<IPlanningRuleRepository, EfPlanningRuleRepository>();
        services.AddScoped<IFrequencyRuleRepository, EfFrequencyRuleRepository>();
        services.AddScoped<GetPlanningRulesQuery>();
        services.AddScoped<CreatePlanningRuleCommand>();
        services.AddScoped<UpdatePlanningRuleCommand>();
        services.AddScoped<DeletePlanningRuleCommand>();
        services.AddScoped<GetFrequencyRulesQuery>();
        services.AddScoped<CreateFrequencyRuleCommand>();
        services.AddScoped<UpdateFrequencyRuleCommand>();
        services.AddScoped<DeleteFrequencyRuleCommand>();

        services.AddScoped<IWeekContextRepository, EfWeekContextRepository>();
        services.AddScoped<GetWeekContextQuery>();
        services.AddScoped<SaveWeekContextCommand>();

        // Jalon 3: Food Items and Open Food Facts
        services.AddScoped<IFoodItemRepository, EfFoodItemRepository>();

        // Open Food Facts HTTP service
        services.AddHttpClient<IOpenFoodFactsService, HttpOpenFoodFactsService>();

        return services;
    }
}
