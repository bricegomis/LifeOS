using LifeOS.Domain.Food.Costs;
using LifeOS.Domain.Food.Products;
using LifeOS.Domain.Food.Recipes;

namespace LifeOS.Domain.Tests.Food.Costs;

public sealed class RecipeCostCalculatorTests
{
    private static readonly Guid Household = Guid.NewGuid();
    private static readonly Guid Leclerc = Guid.NewGuid();
    private static readonly Guid Biocoop = Guid.NewGuid();
    private static readonly DateOnly Day = new(2026, 11, 2);
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);

    private static Product Product(Guid food, string name, decimal quantity, string unit, params (Guid Store, decimal Price)[] prices)
    {
        var product = LifeOS.Domain.Food.Products.Product.Create(Household, food, name, quantity, unit, now: Now);
        foreach (var (store, price) in prices) product.RecordPrice(store, price, Day, Now);
        return product;
    }

    [Fact]
    public void Each_ingredient_takes_the_cheapest_and_dearest_price_across_products_and_stores()
    {
        var lentils = Guid.NewGuid();
        var coconut = Guid.NewGuid();
        var recipe = Recipe.Create(Household, "Dhal", 4, 35);
        var lentilLine = recipe.AddIngredient(lentils, 250, "g");
        recipe.AddIngredient(coconut, 400, "ml");
        var products = new[]
        {
            Product(lentils, "Lentilles 500 g", 500, "g", (Leclerc, 1.80m)),      // 3,60 €/kg
            Product(lentils, "Lentilles vrac", 1, "kg", (Biocoop, 5.90m)),         // 5,90 €/kg
            Product(coconut, "Lait de coco 400 ml", 400, "ml", (Leclerc, 1.28m), (Biocoop, 2.20m)),
        };

        var cost = RecipeCostCalculator.Compute(recipe, products);

        var line = cost.Lines.Single(l => l.IngredientId == lentilLine.Id);
        Assert.Equal(0.90m, line.MinAmount);
        Assert.Equal(1.475m, line.MaxAmount);
        Assert.Equal("Lentilles 500 g", line.Cheapest!.ProductName);
        Assert.Equal(Biocoop, line.Dearest!.StoreId);
        Assert.Equal(0.90m + 1.28m, cost.MinTotal);
        Assert.Equal(1.475m + 2.20m, cost.MaxTotal);
        Assert.True(cost.IsComplete);
        Assert.Equal((0.90m + 1.28m) / 4, cost.MinPerServing);
    }

    [Fact]
    public void An_ingredient_without_comparable_price_stays_unknown_and_makes_the_totals_partial()
    {
        var eggs = Guid.NewGuid();
        var curry = Guid.NewGuid();
        var recipe = Recipe.Create(Household, "Omelette", 2, 10);
        recipe.AddIngredient(eggs, 120, "g");      // eggs are only sold by the piece
        recipe.AddIngredient(curry, 5, "g");       // no product at all
        var products = new[] { Product(eggs, "Œufs x6", 6, "pièce", (Leclerc, 2.10m)) };

        var cost = RecipeCostCalculator.Compute(recipe, products);

        Assert.All(cost.Lines, l => Assert.False(l.IsKnown));
        Assert.Equal(2, cost.MissingIngredients);
        Assert.False(cost.IsComplete);
        Assert.Equal(0, cost.MinTotal);
    }

    [Fact]
    public void Pieces_are_priced_per_piece_and_only_the_latest_price_of_each_store_counts()
    {
        var eggs = Guid.NewGuid();
        var recipe = Recipe.Create(Household, "Œufs durs", 1, 10);
        recipe.AddIngredient(eggs, 2, "pièce");
        var box = Product(eggs, "Œufs x6", 6, "pièce");
        box.RecordPrice(Leclerc, 3.00m, Day.AddDays(-30), Now);
        box.RecordPrice(Leclerc, 2.10m, Day, Now);

        var line = Assert.Single(RecipeCostCalculator.Compute(recipe, [box]).Lines);

        Assert.Equal(0.70m, line.MinAmount);
        Assert.Equal(0.70m, line.MaxAmount);
        Assert.Equal(1, line.ComparablePrices);
    }

    [Fact]
    public void Archived_products_do_not_count()
    {
        var rice = Guid.NewGuid();
        var recipe = Recipe.Create(Household, "Riz", 2, 15);
        recipe.AddIngredient(rice, 300, "g");
        var archived = Product(rice, "Riz", 1, "kg", (Leclerc, 2.40m));
        archived.Archive();

        Assert.Equal(1, RecipeCostCalculator.Compute(recipe, [archived]).MissingIngredients);
    }

    [Fact]
    public void A_recipe_without_ingredients_is_not_complete()
    {
        var cost = RecipeCostCalculator.Compute(Recipe.Create(Household, "Vide", 1, 0), []);
        Assert.False(cost.IsComplete);
        Assert.Equal(0, cost.MissingIngredients);
    }
}
