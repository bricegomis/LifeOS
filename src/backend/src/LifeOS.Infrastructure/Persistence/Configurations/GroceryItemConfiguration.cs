using LifeOS.Domain.Articles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class GroceryItemConfiguration : IEntityTypeConfiguration<GroceryItem>
{
    public void Configure(EntityTypeBuilder<GroceryItem> builder)
    {
        builder.ToTable("articles");

        builder.HasKey(article => article.Id);
        builder.Property(article => article.Id).ValueGeneratedNever();

        builder.Property(article => article.HouseholdId).IsRequired();

        builder.Property(article => article.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(article => article.Description)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(article => article.Unit)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(article => article.CreatedAt).IsRequired();
        builder.Property(article => article.UpdatedAt).IsRequired();

        builder.Metadata.FindNavigation(nameof(GroceryItem.PriceHistory))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(article => article.PriceHistory, priceEntry =>
        {
            priceEntry.ToTable("article_price_entries");

            priceEntry.WithOwner().HasForeignKey("ArticleId");
            priceEntry.HasKey(entry => entry.Id);
            priceEntry.Property(entry => entry.Id).ValueGeneratedNever();

            priceEntry.Property(entry => entry.StoreId).IsRequired();
            priceEntry.Property(entry => entry.Price)
                .HasPrecision(10, 2)
                .IsRequired();
            priceEntry.Property(entry => entry.ObservedAt).IsRequired();
            priceEntry.Property(entry => entry.CreatedAt).IsRequired();

            priceEntry.HasIndex("ArticleId");
        });

        builder.HasOne<LifeOS.Domain.Households.Household>()
            .WithMany()
            .HasForeignKey(article => article.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(article => article.HouseholdId);
    }
}
