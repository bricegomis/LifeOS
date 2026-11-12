using LifeOS.Domain.Food.Recipes;
using LifeOS.Domain.Food.Foods;
using LifeOS.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Food.Recipes;

internal sealed class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.ToTable("recipes", FoodSchema.Name);
        builder.HasKey(recipe => recipe.Id);
        builder.Property(recipe => recipe.Id).ValueGeneratedNever();
        builder.Property(recipe => recipe.HouseholdId).IsRequired();
        builder.HasAlternateKey(recipe => new { recipe.Id, recipe.HouseholdId });
        builder.Property(recipe => recipe.Name).IsRequired().HasMaxLength(Recipe.MaxNameLength);
        builder.Property(recipe => recipe.Servings).IsRequired();
        builder.Property(recipe => recipe.DurationMinutes).IsRequired();
        builder.Property(recipe => recipe.Instructions).HasMaxLength(Recipe.MaxInstructionsLength);
        builder.Property(recipe => recipe.Tags)
            .HasConversion(
                tags => string.Join(Recipe.TagSeparator, tags),
                value => string.IsNullOrEmpty(value) ? new List<string>() : new List<string>(value.Split(Recipe.TagSeparator)))
            .HasMaxLength(1000);
        builder.Property(recipe => recipe.ImagePath).HasMaxLength(500);
        builder.Property(recipe => recipe.ImageContentType).HasMaxLength(100);
        builder.Ignore(recipe => recipe.HasImage);
        builder.Property(recipe => recipe.CreatedAt).IsRequired();
        builder.Property(recipe => recipe.UpdatedAt).IsRequired();

        // Household core: the only cross-module foreign key a domain module may hold.
        builder.HasOne<Household>().WithMany().HasForeignKey(recipe => recipe.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(recipe => recipe.HouseholdId);
        builder.HasMany<RecipeIngredient>().WithOne().HasForeignKey(ingredient => ingredient.RecipeId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> builder)
    {
        builder.ToTable("recipe_ingredients", FoodSchema.Name);
        builder.HasKey(ingredient => ingredient.Id);
        builder.Property(ingredient => ingredient.Id).ValueGeneratedNever();
        builder.Property(ingredient => ingredient.RecipeId).IsRequired();
        builder.Property(ingredient => ingredient.FoodItemId).IsRequired();
        builder.Property(ingredient => ingredient.Quantity).IsRequired().HasPrecision(16, 6);
        builder.Property(ingredient => ingredient.Unit).IsRequired().HasMaxLength(RecipeIngredient.MaxUnitLength);
        builder.HasOne<Recipe>().WithMany(recipe => recipe.Ingredients).HasForeignKey(ingredient => ingredient.RecipeId).OnDelete(DeleteBehavior.Cascade);
        // Same module (Food): a foreign key to the food is allowed.
        builder.HasOne<FoodItem>().WithMany().HasForeignKey(ingredient => ingredient.FoodItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(ingredient => ingredient.RecipeId);
        builder.HasIndex(ingredient => ingredient.FoodItemId);
    }
}
