using LifeOS.Domain.Recipes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> builder)
    {
        builder.ToTable("recipe_ingredients");

        builder.HasKey(ingredient => ingredient.Id);
        builder.Property(ingredient => ingredient.Id).ValueGeneratedNever();

        builder.Property(ingredient => ingredient.RecipeId).IsRequired();
        builder.Property(ingredient => ingredient.FoodItemId).IsRequired();
        builder.Property(ingredient => ingredient.Quantity).IsRequired().HasPrecision(10, 2);
        builder.Property(ingredient => ingredient.Unit)
            .IsRequired()
            .HasMaxLength(50);

        // FK to Recipe
        builder.HasOne<Recipe>()
            .WithMany(r => r.Ingredients)
            .HasForeignKey(ingredient => ingredient.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to GroceryItem (food_item)
        builder.HasOne<LifeOS.Domain.Articles.GroceryItem>()
            .WithMany()
            .HasForeignKey(ingredient => ingredient.FoodItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(ingredient => ingredient.RecipeId);
        builder.HasIndex(ingredient => ingredient.FoodItemId);
    }
}
