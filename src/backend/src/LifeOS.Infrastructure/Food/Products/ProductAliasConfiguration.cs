using LifeOS.Domain.Food.Foods;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Food.Products;

internal sealed class ProductAliasConfiguration : IEntityTypeConfiguration<ProductAlias>
{
    public void Configure(EntityTypeBuilder<ProductAlias> builder)
    {
        builder.ToTable("product_aliases");
        builder.HasKey(a => a.ArticleId);
        builder.HasOne<LifeOS.Domain.Food.Products.Product>().WithMany().HasForeignKey(a => new { a.ProductId, a.HouseholdId })
            .HasPrincipalKey(p => new { p.Id, p.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
    }
}
