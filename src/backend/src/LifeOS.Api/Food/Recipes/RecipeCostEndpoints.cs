using LifeOS.Api.Households;
using LifeOS.Application.Food.Costs;
using LifeOS.Domain.Food;
using LifeOS.Domain.Food.Costs;

namespace LifeOS.Api.Food.Recipes;

public sealed record PriceCandidateDto(Guid ProductId, string ProductName, Guid StoreId, decimal PackagePrice,
    decimal UnitPrice, string UnitLabel, DateOnly ObservedOn)
{
    public static PriceCandidateDto? From(PriceCandidate? c) => c is null ? null
        : new(c.ProductId, c.ProductName, c.StoreId, c.PackagePrice, decimal.Round(c.UnitPrice, 4), Measures.BaseUnitLabel(c.Dimension), c.ObservedOn);
}

public sealed record IngredientCostDto(Guid IngredientId, Guid FoodItemId, decimal Quantity, string Unit,
    decimal? MinAmount, decimal? MaxAmount, PriceCandidateDto? Cheapest, PriceCandidateDto? Dearest, int ComparablePrices);

/// <summary>Totals add only known ingredients: they are partial while <c>MissingIngredients</c> is above zero.</summary>
public sealed record RecipeCostDto(Guid RecipeId, int Servings, decimal MinTotal, decimal MaxTotal,
    decimal MinPerServing, decimal MaxPerServing, int MissingIngredients, bool IsComplete, List<IngredientCostDto> Lines)
{
    public static RecipeCostDto From(RecipeCost cost) => new(cost.RecipeId, cost.Servings,
        decimal.Round(cost.MinTotal, 2), decimal.Round(cost.MaxTotal, 2),
        decimal.Round(cost.MinPerServing, 2), decimal.Round(cost.MaxPerServing, 2),
        cost.MissingIngredients, cost.IsComplete,
        cost.Lines.Select(line => new IngredientCostDto(line.IngredientId, line.FoodItemId, line.Quantity, line.Unit,
            line.MinAmount is { } min ? decimal.Round(min, 2) : null, line.MaxAmount is { } max ? decimal.Round(max, 2) : null,
            PriceCandidateDto.From(line.Cheapest), PriceCandidateDto.From(line.Dearest), line.ComparablePrices)).ToList());
}

/// <summary>Min–max recipe costs (ADR 0010, § 5), computed on read by <see cref="RecipeCosts"/>.</summary>
public static class RecipeCostEndpoints
{
    public static IEndpointRouteBuilder MapRecipeCostEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/recipes").WithTags("Recipes").RequireAuthorization();
        group.MapGet("/costs", All).WithName("GetRecipeCosts").Produces<List<RecipeCostDto>>();
        group.MapGet("/{recipeId:guid}/cost", One).WithName("GetRecipeCost").Produces<RecipeCostDto>().ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }

    private static async Task<IResult> All(HouseholdContext household, RecipeCosts costs, CancellationToken ct) =>
        Results.Ok((await costs.OfAllRecipesAsync(household.HouseholdId, ct)).Select(RecipeCostDto.From).ToList());

    private static async Task<IResult> One(Guid recipeId, HouseholdContext household, RecipeCosts costs, CancellationToken ct) =>
        await costs.OfRecipeAsync(household.HouseholdId, recipeId, ct) is { } cost ? Results.Ok(RecipeCostDto.From(cost)) : Results.NotFound();
}
