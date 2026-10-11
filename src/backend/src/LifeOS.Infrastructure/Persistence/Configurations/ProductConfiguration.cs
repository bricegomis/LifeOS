using LifeOS.Domain.Products;
using LifeOS.Domain.FoodItems;
using LifeOS.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("products");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(p => p.HouseholdId).HasColumnName("household_id");
        b.HasAlternateKey(p => new { p.Id, p.HouseholdId });
        b.Property(p => p.Name).HasColumnName("name").HasMaxLength(500);
        b.Property(p => p.Description).HasMaxLength(1000);
        b.Property(p => p.Brand).HasMaxLength(200);
        b.Property(p => p.Barcode).HasMaxLength(50);
        b.Property(p => p.PurchaseUnit).HasMaxLength(20);
        b.Property(p => p.PackageUnit).HasMaxLength(20);
        b.Property(p => p.PackageQuantity).HasPrecision(16, 6);
        b.Property(p => p.CreatedAt).HasColumnName("created_at");
        b.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        b.Property(p => p.LegacyPurchaseName).HasMaxLength(500);
        b.Property(p => p.MigrationOrigin).HasMaxLength(20);
        b.HasOne<Household>().WithMany().HasForeignKey(p => p.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FoodItem>().WithMany().HasForeignKey(p => new { p.FoodItemId, p.HouseholdId })
            .HasPrincipalKey(f => new { f.Id, f.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        b.OwnsMany(p => p.PriceHistory, e =>
        {
            e.ToTable("article_price_entries");
            e.WithOwner().HasForeignKey("ArticleId");
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).ValueGeneratedNever();
            e.Property(p => p.Price).HasPrecision(16, 6);
            e.Property(p => p.PackageQuantity).HasPrecision(16, 6);
            e.Property(p => p.Unit).HasMaxLength(20);
            e.Property(p => p.PackageUnit).HasMaxLength(20);
            e.HasIndex(p => p.ReceiptLineId).IsUnique();
            e.Ignore(p => p.Provenance);
        });
        b.Metadata.FindNavigation(nameof(Product.PriceHistory))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
