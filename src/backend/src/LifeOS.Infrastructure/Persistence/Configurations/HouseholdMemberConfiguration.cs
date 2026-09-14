using LifeOS.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class HouseholdMemberConfiguration : IEntityTypeConfiguration<HouseholdMember>
{
    public void Configure(EntityTypeBuilder<HouseholdMember> builder)
    {
        builder.ToTable("household_members");

        builder.HasKey(member => member.Id);
        builder.Property(member => member.Id).ValueGeneratedNever();

        builder.Property(member => member.HouseholdId).IsRequired();
        builder.Property(member => member.SupabaseUserId).IsRequired();
        builder.Property(member => member.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(member => member.CreatedAt).IsRequired();

        // MVP invariant (ADR 0003): a Supabase user resolves to at most one active household.
        builder.HasIndex(member => member.SupabaseUserId).IsUnique();
    }
}
