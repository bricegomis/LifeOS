using LifeOS.Api.Food.Foods;
using LifeOS.Api.Food.Products;
using LifeOS.Api.Food.Recipes;
using LifeOS.Api.Food.Reviews;
using LifeOS.Api.Food.Stores;

namespace LifeOS.Api.Food;

public static class FoodModule
{
    /// <summary>Management code shared by the Food endpoints and the assistant tools (ADR 0011).</summary>
    public static IServiceCollection AddFoodLibraries(this IServiceCollection services) => services
        .AddScoped<FoodLibrary>()
        .AddScoped<RecipeLibrary>()
        .AddScoped<StoreLibrary>()
        .AddScoped<ProductLibrary>()
        .AddScoped<ReviewLibrary>();
}
