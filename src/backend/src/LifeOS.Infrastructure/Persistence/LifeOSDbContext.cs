using LifeOS.Domain.Budget;
using LifeOS.Domain.Food.Foods;
using LifeOS.Domain.Food.Products;
using LifeOS.Domain.Food.Receipts;
using LifeOS.Domain.Food.Recipes;
using LifeOS.Domain.Food.Reviews;
using LifeOS.Domain.Food.Stores;
using LifeOS.Domain.Households;
using LifeOS.Domain.Sports;
using LifeOS.Domain.Health;
using LifeOS.Domain.Reading;
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
    public DbSet<ImportToken> ImportTokens => Set<ImportToken>();
    public DbSet<GarminLink> GarminLinks => Set<GarminLink>();

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
    public DbSet<Vo2MaxMeasure> Vo2MaxMeasures => Set<Vo2MaxMeasure>();

    // Tasks module (ADR 0017)
    public DbSet<Todo> Todos => Set<Todo>();
    public DbSet<TaskRoutine> TaskRoutines => Set<TaskRoutine>();
    public DbSet<TaskProject> TaskProjects => Set<TaskProject>();

    // Health module (ADR 0018): private per account and profile
    public DbSet<Supplement> Supplements => Set<Supplement>();
    public DbSet<SymptomType> SymptomTypes => Set<SymptomType>();
    public DbSet<SymptomEntry> SymptomEntries => Set<SymptomEntry>();
    public DbSet<CalmDay> CalmDays => Set<CalmDay>();
    public DbSet<Marker> Markers => Set<Marker>();
    public DbSet<Analysis> Analyses => Set<Analysis>();
    public DbSet<MeasureType> MeasureTypes => Set<MeasureType>();
    public DbSet<Measurement> Measurements => Set<Measurement>();
    public DbSet<HealthDocument> HealthDocuments => Set<HealthDocument>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<DailyWellness> DailyWellness => Set<DailyWellness>();

    // Reading module (ADR 0022)
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookSeries> Series => Set<BookSeries>();

    // Budget module (ADR 0020): private per account, reads published contracts only
    public DbSet<BudgetLine> BudgetLines => Set<BudgetLine>();
    public DbSet<BudgetOperation> BudgetOperations => Set<BudgetOperation>();
    public DbSet<BudgetRule> BudgetRules => Set<BudgetRule>();
    public DbSet<BudgetAccount> BudgetAccounts => Set<BudgetAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LifeOSDbContext).Assembly);
        OpenIddictModel.Configure(modelBuilder);
    }
}
