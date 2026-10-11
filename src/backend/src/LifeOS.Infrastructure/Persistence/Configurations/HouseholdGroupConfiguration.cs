using LifeOS.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class HouseholdGroupConfiguration : IEntityTypeConfiguration<HouseholdGroup>
{
    public void Configure(EntityTypeBuilder<HouseholdGroup> builder)
    {
        builder.ToTable("household_groups");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();
        builder.Property(g => g.Name).HasMaxLength(100).IsRequired();
        builder.HasAlternateKey(g => new { g.Id, g.HouseholdId });
        builder.HasOne<Household>().WithMany().HasForeignKey(g => g.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(g => new { g.HouseholdId, g.Name }).IsUnique();
    }
}
