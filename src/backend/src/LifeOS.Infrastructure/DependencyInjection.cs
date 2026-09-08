using LifeOS.Application.Articles;
using LifeOS.Application.Common.Interfaces;
using LifeOS.Application.Library;
using LifeOS.Application.Planning;
using LifeOS.Application.Stores;
using LifeOS.Application.WeekContexts;
using LifeOS.Infrastructure.Articles;
using LifeOS.Infrastructure.Library;
using LifeOS.Infrastructure.Planning;
using LifeOS.Infrastructure.Stores;
using LifeOS.Infrastructure.WeekContexts;
using Microsoft.Extensions.DependencyInjection;

namespace LifeOS.Infrastructure;

/// <summary>
/// Wires the Infrastructure layer's implementations behind the Application layer's ports.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IStoreRepository, InMemoryStoreRepository>();
        services.AddScoped<GetStoresQuery>();

        services.AddSingleton<IArticleRepository, InMemoryArticleRepository>();
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
