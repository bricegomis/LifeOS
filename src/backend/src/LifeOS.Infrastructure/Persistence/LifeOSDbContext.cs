using LifeOS.Domain.Articles;
using LifeOS.Domain.ComposedMeals;
using LifeOS.Domain.FoodItems;
using LifeOS.Domain.Households;
using LifeOS.Domain.Recipes;
using LifeOS.Domain.Stock;
using LifeOS.Domain.Stores;
using LifeOS.Domain.WeekPlanning;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Persistence;

/// <summary>
/// EF Core / Npgsql database context backing the PostgreSQL business database (ADR 0001).
/// Includes Jalon 1 (households, stores, articles), Jalon 2 (recipes, composed meals, week planning),
/// Jalon 3 (food items, Open Food Facts integration), Jalon 4 (nutrition/sports context),
/// and Jalon 6 (stock and shopping list).
/// </summary>
public sealed class LifeOSDbContext(DbContextOptions<LifeOSDbContext> options) : DbContext(options)
{
    // Jalon 1: Households and Articles
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
    public DbSet<MemberProfile> MemberProfiles => Set<MemberProfile>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<GroceryItem> GroceryItems => Set<GroceryItem>();

    // Jalon 2: Recipes and Meals
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<ComposedMeal> ComposedMeals => Set<ComposedMeal>();
    public DbSet<ComposedMealPart> ComposedMealParts => Set<ComposedMealPart>();

    // Jalon 2: Week Planning
    public DbSet<Week> Weeks => Set<Week>();
    public DbSet<DayPlan> DayPlans => Set<DayPlan>();
    public DbSet<PlannedMeal> PlannedMeals => Set<PlannedMeal>();
    public DbSet<PlannedMealPart> PlannedMealParts => Set<PlannedMealPart>();
    public DbSet<WeekScenario> WeekScenarios => Set<WeekScenario>();

    // Jalon 3: Food Items
    public DbSet<FoodItem> FoodItems => Set<FoodItem>();

    // Jalon 4: Nutrition and Sports Context
    public DbSet<UserConfiguration> UserConfigurations => Set<UserConfiguration>();
    public DbSet<ActivitySession> ActivitySessions => Set<ActivitySession>();

    // Jalon 6: Stock and Shopping List
    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LifeOSDbContext).Assembly);
    }
}
