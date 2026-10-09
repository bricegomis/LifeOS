using LifeOS.Domain.FoodItems;
using LifeOS.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="FoodItem"/> entity (Jalon 3).
/// </summary>
public sealed class FoodItemConfiguration : IEntityTypeConfiguration<FoodItem>
{
    public void Configure(EntityTypeBuilder<FoodItem> builder)
    {
        builder.ToTable("products");
        builder.Property(f => f.IsArchived).HasDefaultValue(false);
        builder.Ignore(f => f.ArticleId);
        builder.Property(f => f.Description).HasMaxLength(1000).IsRequired();
        builder.Property(f => f.Unit).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(f => f.LegacyPurchaseName).HasMaxLength(500);
        builder.Property(f => f.MigrationOrigin).HasMaxLength(20);
        builder.OwnsMany(f => f.PriceHistory, price =>
        {
            price.ToTable("article_price_entries");
            price.WithOwner().HasForeignKey("ArticleId");
            price.HasKey(e => e.Id);
            price.Property(e => e.Id).ValueGeneratedNever();
            price.Property(e => e.Price).HasPrecision(10, 2).IsRequired();
            price.HasIndex("ArticleId");
        });
        builder.Metadata.FindNavigation(nameof(FoodItem.PriceHistory))!.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(f => f.HouseholdId)
            .HasColumnName("household_id")
            .IsRequired();

        builder.Property(f => f.Name)
            .HasColumnName("name")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(f => f.ReferenceUnit)
            .HasColumnName("reference_unit")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(f => f.Source)
            .HasColumnName("source")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(f => f.OffBarcode)
            .HasColumnName("off_barcode")
            .HasMaxLength(50);

        builder.Property(f => f.OffPayload)
            .HasColumnName("off_payload")
            .HasColumnType("jsonb");

        builder.Property(f => f.IsCorrectionOf)
            .HasColumnName("is_correction_of");

        builder.Property(f => f.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(f => f.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Configure Nutrition as a complex type (value object)
        builder.OwnsOne(
            f => f.Nutrition,
            nutrition =>
            {
                nutrition.Property(n => n.CaloriesPerUnit).HasColumnName("calories_per_unit");
                nutrition.Property(n => n.ProteinsPerUnit).HasColumnName("proteins_per_unit");
                nutrition.Property(n => n.CarbsPerUnit).HasColumnName("carbs_per_unit");
                nutrition.Property(n => n.FatsPerUnit).HasColumnName("fats_per_unit");
            });

        // Isolation by household (ADR 0003): FK constraint + index for scoped lookups.
        builder.HasOne<Household>()
            .WithMany()
            .HasForeignKey(f => f.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        // Self-referencing FK for manual corrections of another food item (nullable: not every
        // item is a correction). SetNull rather than Cascade/Restrict so deleting the original
        // item does not cascade-delete or block deletion of the correction that references it.
        builder.HasOne<FoodItem>()
            .WithMany()
            .HasForeignKey(f => f.IsCorrectionOf)
            .OnDelete(DeleteBehavior.SetNull);

        // Index for household isolation
        builder.HasIndex(f => f.HouseholdId)
            .HasDatabaseName("idx_food_items_household_id");

        // Index for OFF barcode lookup
        builder.HasIndex(f => new { f.HouseholdId, f.OffBarcode })
            .HasDatabaseName("idx_food_items_household_off_barcode");

        builder.HasIndex(f => f.IsCorrectionOf)
            .HasDatabaseName("idx_food_items_is_correction_of");
    }
}
