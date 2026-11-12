using LifeOS.Domain.Food.Foods;
using LifeOS.Domain.Food.Products;
using LifeOS.Domain.Food.Stores;
using LifeOS.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Food.Products;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("products", FoodSchema.Name, t =>
        {
            t.HasCheckConstraint("ck_product_package_quantity", "package_quantity > 0");
            t.HasCheckConstraint("ck_product_package_unit", "package_unit IN ('g', 'kg', 'ml', 'L', 'pièce')");
        });
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(p => p.HouseholdId).HasColumnName("household_id");
        b.HasAlternateKey(p => new { p.Id, p.HouseholdId });
        b.Property(p => p.FoodItemId).HasColumnName("food_id");
        b.Property(p => p.Name).HasColumnName("name").HasMaxLength(Product.MaxNameLength).IsRequired();
        b.Property(p => p.Brand).HasColumnName("brand").HasMaxLength(100);
        b.Property(p => p.Barcode).HasColumnName("barcode").HasMaxLength(50);
        b.Property(p => p.PackageQuantity).HasColumnName("package_quantity").HasPrecision(16, 6);
        b.Property(p => p.PackageUnit).HasColumnName("package_unit").HasMaxLength(10).IsRequired();
        b.Property(p => p.IsArchived).HasColumnName("is_archived");
        b.Property(p => p.CreatedAt).HasColumnName("created_at");
        b.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        b.HasOne<Household>().WithMany().HasForeignKey(p => p.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<FoodItem>().WithMany().HasForeignKey(p => new { p.FoodItemId, p.HouseholdId })
            .HasPrincipalKey(f => new { f.Id, f.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(p => p.Prices).WithOne().HasForeignKey(p => new { p.ProductId, p.HouseholdId })
            .HasPrincipalKey(p => new { p.Id, p.HouseholdId }).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(p => new { p.HouseholdId, p.FoodItemId });
    }
}

internal sealed class ProductPriceConfiguration : IEntityTypeConfiguration<ProductPrice>
{
    public void Configure(EntityTypeBuilder<ProductPrice> b)
    {
        b.ToTable("product_prices", FoodSchema.Name, t =>
        {
            t.HasCheckConstraint("ck_product_price_positive", "price > 0");
            t.HasCheckConstraint("ck_product_price_package", "package_quantity > 0");
        });
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(p => p.ProductId).HasColumnName("product_id");
        b.Property(p => p.HouseholdId).HasColumnName("household_id");
        b.Property(p => p.StoreId).HasColumnName("store_id");
        b.Property(p => p.Price).HasColumnName("price").HasPrecision(10, 2);
        b.Property(p => p.ObservedOn).HasColumnName("observed_on");
        b.Property(p => p.PackageQuantity).HasColumnName("package_quantity").HasPrecision(16, 6);
        b.Property(p => p.PackageUnit).HasColumnName("package_unit").HasMaxLength(10).IsRequired();
        b.Property(p => p.CreatedAt).HasColumnName("created_at");
        b.Ignore(p => p.Basis);
        b.Ignore(p => p.UnitPrice);
        b.HasOne<Store>().WithMany().HasForeignKey(p => new { p.StoreId, p.HouseholdId })
            .HasPrincipalKey(s => new { s.Id, s.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(p => new { p.ProductId, p.StoreId, p.ObservedOn });
    }
}
