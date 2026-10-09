using LifeOS.Domain.FoodItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class ProductAliasConfiguration : IEntityTypeConfiguration<ProductAlias>
{
    public void Configure(EntityTypeBuilder<ProductAlias> builder)
    {
        builder.ToTable("product_aliases");
        builder.HasKey(a => a.ArticleId);
        builder.HasOne<FoodItem>().WithMany().HasForeignKey(a => new { a.ProductId, a.HouseholdId })
            .HasPrincipalKey(p => new { p.Id, p.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
    }
}
