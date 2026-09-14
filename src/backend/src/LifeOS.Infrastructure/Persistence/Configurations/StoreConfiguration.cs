using LifeOS.Domain.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> builder)
    {
        builder.ToTable("stores");

        builder.HasKey(store => store.Id);
        builder.Property(store => store.Id).ValueGeneratedNever();

        builder.Property(store => store.HouseholdId).IsRequired();

        builder.Property(store => store.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(store => store.Address)
            .IsRequired()
            .HasMaxLength(400);

        builder.Property(store => store.IsOrganic).IsRequired();
        builder.Property(store => store.IsLocal).IsRequired();
        builder.Property(store => store.CreatedAt).IsRequired();
        builder.Property(store => store.UpdatedAt).IsRequired();

        // Isolation by household (ADR 0003): FK constraint + index for scoped lookups.
        builder.HasOne<LifeOS.Domain.Households.Household>()
            .WithMany()
            .HasForeignKey(store => store.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(store => store.HouseholdId);
    }
}
