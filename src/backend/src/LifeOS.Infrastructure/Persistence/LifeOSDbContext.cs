using LifeOS.Domain.Articles;
using LifeOS.Domain.FoodItems;
using LifeOS.Domain.Households;
using LifeOS.Domain.Recipes;
using LifeOS.Domain.Products;
using LifeOS.Domain.Receipts;
using LifeOS.Domain.Stores;
using LifeOS.Domain.WeekPlanning;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Persistence;

/// <summary>
/// EF Core / Npgsql database context for active household, food, product, receipt,
/// recipe, manual-planning and sport data.
/// </summary>
public sealed class LifeOSDbContext(DbContextOptions<LifeOSDbContext> options) : DbContext(options)
{
    // Households and household reviews
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
    public DbSet<MemberProfile> MemberProfiles => Set<MemberProfile>();
    public DbSet<HouseholdGroup> HouseholdGroups => Set<HouseholdGroup>();
    public DbSet<FoodReview> FoodReviews => Set<FoodReview>();
    public DbSet<RecipeReview> RecipeReviews => Set<RecipeReview>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ReceiptLine> ReceiptLines => Set<ReceiptLine>();
    public DbSet<Purchase> Purchases => Set<Purchase>();

    // Recipes and planned meals
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();

    // Manual week planning
    public DbSet<Week> Weeks => Set<Week>();
    public DbSet<DayPlan> DayPlans => Set<DayPlan>();
    public DbSet<PlannedMeal> PlannedMeals => Set<PlannedMeal>();
    public DbSet<PlannedRecipeLine> PlannedRecipeLines => Set<PlannedRecipeLine>();
    public DbSet<PlannedRecipeLineIngredient> PlannedRecipeLineIngredients => Set<PlannedRecipeLineIngredient>();
    public DbSet<PlannedRecipeLinePortion> PlannedRecipeLinePortions => Set<PlannedRecipeLinePortion>();

    // Foods and product aliases
    public DbSet<FoodItem> FoodItems => Set<FoodItem>();
    public DbSet<ProductAlias> ProductAliases => Set<ProductAlias>();

    public async Task<Guid> ResolveProductIdAsync(Guid householdId, Guid id, CancellationToken ct)
    {
        // Exact identities only. No name-based matching.
        return await ProductAliases.Where(a => a.ArticleId == id && a.HouseholdId == householdId)
            .Select(a => (Guid?)a.ProductId).SingleOrDefaultAsync(ct) ?? id;
    }

    // Nutrition configuration and sport
    public DbSet<UserConfiguration> UserConfigurations => Set<UserConfiguration>();
    public DbSet<ActivitySession> ActivitySessions => Set<ActivitySession>();
    public DbSet<SportTemplate> SportTemplates => Set<SportTemplate>();

    // Planning configuration
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LifeOSDbContext).Assembly);
    }
}
