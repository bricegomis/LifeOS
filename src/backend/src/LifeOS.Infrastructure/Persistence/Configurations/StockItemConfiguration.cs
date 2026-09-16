using LifeOS.Domain.Stock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> builder)
    {
        builder.ToTable("stock_items");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();

        builder.Property(item => item.HouseholdId).IsRequired();
        builder.Property(item => item.GroceryItemId).IsRequired();

        builder.Property(item => item.Quantity)
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(item => item.Unit)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(item => item.CreatedAt).IsRequired();
        builder.Property(item => item.UpdatedAt).IsRequired();

        builder.HasOne<LifeOS.Domain.Households.Household>()
            .WithMany()
            .HasForeignKey(item => item.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<LifeOS.Domain.Articles.GroceryItem>()
            .WithMany()
            .HasForeignKey(item => item.GroceryItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => item.HouseholdId);
        builder.HasIndex(item => item.GroceryItemId);
        builder.HasIndex(item => new { item.HouseholdId, item.GroceryItemId }).IsUnique();
    }
}
