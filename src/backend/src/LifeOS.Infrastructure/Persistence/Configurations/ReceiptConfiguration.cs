using LifeOS.Domain.Households;
using LifeOS.Domain.Receipts;
using LifeOS.Domain.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

public sealed class ReceiptConfiguration : IEntityTypeConfiguration<Receipt>
{
    public void Configure(EntityTypeBuilder<Receipt> b)
    {
        b.ToTable("receipts", table =>
            table.HasCheckConstraint("ck_receipt_status", "\"Status\" IN ('pending', 'processed')"));
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).ValueGeneratedNever();
        b.HasAlternateKey(r => new { r.Id, r.HouseholdId });
        b.HasIndex(r => new { r.HouseholdId, r.DocumentHash }).IsUnique();
        b.Property(r => r.DocumentHash).HasMaxLength(64);
        b.Property(r => r.Status).HasMaxLength(20).IsRequired();
        b.Property(r => r.ImagePath).HasMaxLength(500).IsRequired();
        b.Property(r => r.ImageContentType).HasMaxLength(100).IsRequired();
        b.HasOne<Household>().WithMany().HasForeignKey(r => r.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Store>().WithMany().HasForeignKey(r => new { r.StoreId, r.HouseholdId })
            .HasPrincipalKey(s => new { s.Id, s.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
    }
}
