using LifeOS.Application.Common.Interfaces;
using LifeOS.Application.Stores;
using LifeOS.Infrastructure.Stores;
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

        return services;
    }
}
