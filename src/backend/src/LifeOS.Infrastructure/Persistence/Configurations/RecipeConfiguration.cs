using LifeOS.Domain.Recipes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.ToTable("recipes");

        builder.HasKey(recipe => recipe.Id);
        builder.Property(recipe => recipe.Id).ValueGeneratedNever();

        builder.Property(recipe => recipe.HouseholdId).IsRequired();

        builder.Property(recipe => recipe.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(recipe => recipe.Servings).IsRequired();
        builder.Property(recipe => recipe.DurationMinutes).IsRequired();

        builder.Property(recipe => recipe.Tags)
            .HasConversion(
                tags => string.Join("|", tags),
                value => string.IsNullOrEmpty(value) 
                    ? new List<string>() 
                    : new List<string>(value.Split("|")))
            .HasMaxLength(1000);

        builder.Property(recipe => recipe.Metadata)
            .HasColumnType("jsonb")
            .IsRequired(false);

        builder.Property(recipe => recipe.CreatedAt).IsRequired();
        builder.Property(recipe => recipe.UpdatedAt).IsRequired();

        // Isolation by household
        builder.HasOne<LifeOS.Domain.Households.Household>()
            .WithMany()
            .HasForeignKey(recipe => recipe.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(recipe => recipe.HouseholdId);

        // Navigation for ingredients
        builder.HasMany<RecipeIngredient>()
            .WithOne()
            .HasForeignKey(ingredient => ingredient.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
