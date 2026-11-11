using LifeOS.Domain.Articles;
using LifeOS.Domain.FoodItems;
using LifeOS.Domain.Households;
using LifeOS.Domain.Recipes;
using LifeOS.Domain.Products;
using LifeOS.Domain.Receipts;
using LifeOS.Domain.Stores;
using LifeOS.Domain.Sports;
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
    public DbSet<FoodReview> FoodReviews => Set<FoodReview>();
    public DbSet<RecipeReview> RecipeReviews => Set<RecipeReview>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Receipt> Receipts => Set<Receipt>();

    // Recipes and planned meals
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();

    // Manual week planning

    // Foods and product aliases
    public DbSet<FoodItem> FoodItems => Set<FoodItem>();
    public DbSet<ProductAlias> ProductAliases => Set<ProductAlias>();

    // Sport sessions and templates
    public DbSet<SportTemplate> SportTemplates => Set<SportTemplate>();

    // Planning configuration
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LifeOSDbContext).Assembly);
    }
}
