using LifeOS.Domain.Food.Foods;
using LifeOS.Domain.Food.Products;
using LifeOS.Domain.Food.Receipts;
using LifeOS.Domain.Food.Recipes;
using LifeOS.Domain.Food.Reviews;
using LifeOS.Domain.Food.Stores;
using LifeOS.Domain.Households;
using LifeOS.Domain.Sports;
using LifeOS.Domain.Health;
using LifeOS.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Persistence;

/// <summary>
/// Single EF Core context of the modular monolith. Each module owns its sets and its
/// configurations (<c>Infrastructure/&lt;Module&gt;</c>); a module only queries its own sets
/// and the household core.
/// </summary>
public sealed class LifeOSDbContext(DbContextOptions<LifeOSDbContext> options) : DbContext(options)
{
    // Household core
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
    public DbSet<MemberProfile> MemberProfiles => Set<MemberProfile>();

    // Food module
    public DbSet<FoodItem> FoodItems => Set<FoodItem>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<FoodReview> FoodReviews => Set<FoodReview>();
    public DbSet<RecipeReview> RecipeReviews => Set<RecipeReview>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductPrice> ProductPrices => Set<ProductPrice>();
    public DbSet<Receipt> Receipts => Set<Receipt>();

    // Sports module
    public DbSet<SportTemplate> SportTemplates => Set<SportTemplate>();
    public DbSet<SportSession> SportSessions => Set<SportSession>();
    public DbSet<WeeklyGoal> WeeklyGoals => Set<WeeklyGoal>();
    public DbSet<DailyChallenge> DailyChallenges => Set<DailyChallenge>();
    public DbSet<ChallengeEntry> ChallengeEntries => Set<ChallengeEntry>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<Routine> Routines => Set<Routine>();
    public DbSet<ExerciseCompletion> ExerciseCompletions => Set<ExerciseCompletion>();
    public DbSet<RoutineSession> RoutineSessions => Set<RoutineSession>();
    public DbSet<SportProfileSettings> SportProfileSettings => Set<SportProfileSettings>();

    // Tasks module (ADR 0017)
    public DbSet<Todo> Todos => Set<Todo>();
    public DbSet<TaskRoutine> TaskRoutines => Set<TaskRoutine>();
    public DbSet<TaskProject> TaskProjects => Set<TaskProject>();

    // Health module (ADR 0018): private per account and profile
    public DbSet<Supplement> Supplements => Set<Supplement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LifeOSDbContext).Assembly);
        OpenIddictModel.Configure(modelBuilder);
    }
}
