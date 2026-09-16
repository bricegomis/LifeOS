using LifeOS.Domain.ComposedMeals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class ComposedMealPartConfiguration : IEntityTypeConfiguration<ComposedMealPart>
{
    public void Configure(EntityTypeBuilder<ComposedMealPart> builder)
    {
        builder.ToTable("composed_meal_parts");

        builder.HasKey(part => part.Id);
        builder.Property(part => part.Id).ValueGeneratedNever();

        builder.Property(part => part.ComposedMealId).IsRequired();
        builder.Property(part => part.RecipeId).IsRequired();
        builder.Property(part => part.QuantityFactor).IsRequired().HasPrecision(5, 2);

        // FK to ComposedMeal
        builder.HasOne<ComposedMeal>()
            .WithMany()
            .HasForeignKey(part => part.ComposedMealId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to Recipe
        builder.HasOne<LifeOS.Domain.Recipes.Recipe>()
            .WithMany()
            .HasForeignKey(part => part.RecipeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(part => part.ComposedMealId);
        builder.HasIndex(part => part.RecipeId);
    }
}
