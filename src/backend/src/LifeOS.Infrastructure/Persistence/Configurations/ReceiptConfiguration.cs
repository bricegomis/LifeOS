using LifeOS.Domain.Receipts;
using LifeOS.Domain.Products;
using LifeOS.Domain.Stores;
using LifeOS.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

public sealed class ReceiptConfiguration : IEntityTypeConfiguration<Receipt>
{
    public void Configure(EntityTypeBuilder<Receipt> b)
    {
        b.ToTable("receipts");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).ValueGeneratedNever();
        b.HasAlternateKey(r => new { r.Id, r.HouseholdId });
        b.HasIndex(r => new { r.HouseholdId, r.DocumentHash }).IsUnique();
        b.Property(r => r.DocumentHash).HasMaxLength(64);
        b.Ignore(r => r.CompletedLines);
        b.HasOne<Household>().WithMany().HasForeignKey(r => r.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Store>().WithMany().HasForeignKey(r => new { r.StoreId, r.HouseholdId })
            .HasPrincipalKey(s => new { s.Id, s.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(r => r.Lines).WithOne().HasForeignKey(l => new { l.ReceiptId, l.HouseholdId })
            .HasPrincipalKey(r => new { r.Id, r.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ReceiptLineConfiguration : IEntityTypeConfiguration<ReceiptLine>
{
    public void Configure(EntityTypeBuilder<ReceiptLine> b)
    {
        b.ToTable("receipt_lines");
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).ValueGeneratedNever();
        b.HasAlternateKey(l => new { l.Id, l.HouseholdId });
        b.HasIndex(l => new { l.ReceiptId, l.Position }).IsUnique();
        b.Ignore(l => l.Diagnostics);
        b.Ignore(l => l.HasPriceMismatch);
        b.Property(l => l.Total).HasPrecision(16, 6);
        b.Property(l => l.Quantity).HasPrecision(16, 6);
        b.Property(l => l.StatedUnitPrice).HasPrecision(16, 6);
        b.HasOne<Product>().WithMany().HasForeignKey(l => new { l.ProductId, l.HouseholdId })
            .HasPrincipalKey(p => new { p.Id, p.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> b)
    {
        b.ToTable("purchases");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).ValueGeneratedNever();
        b.HasIndex(p => p.ReceiptLineId).IsUnique();
        b.Property(p => p.Total).HasPrecision(16, 2);
        b.Property(p => p.Quantity).HasPrecision(16, 6);
        b.HasOne<ReceiptLine>().WithMany().HasForeignKey(p => new { p.ReceiptLineId, p.HouseholdId })
            .HasPrincipalKey(l => new { l.Id, l.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Product>().WithMany().HasForeignKey(p => new { p.ProductId, p.HouseholdId })
            .HasPrincipalKey(p => new { p.Id, p.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Store>().WithMany().HasForeignKey(p => new { p.StoreId, p.HouseholdId })
            .HasPrincipalKey(s => new { s.Id, s.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
    }
}
