using LifeOS.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class HouseholdConfiguration : IEntityTypeConfiguration<Household>
{
    public void Configure(EntityTypeBuilder<Household> builder)
    {
        builder.ToTable("households");

        builder.HasKey(household => household.Id);
        builder.Property(household => household.Id).ValueGeneratedNever();

        builder.Property(household => household.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(household => household.CreatedAt).IsRequired();
        builder.Property(household => household.UpdatedAt).IsRequired();

        builder.Metadata.FindNavigation(nameof(Household.Members))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Household.MemberProfiles))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(household => household.Members)
            .WithOne()
            .HasForeignKey(member => member.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(household => household.MemberProfiles)
            .WithOne()
            .HasForeignKey(profile => profile.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
