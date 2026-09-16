using LifeOS.Domain.WeekPlanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class PlannedMealPartConfiguration : IEntityTypeConfiguration<PlannedMealPart>
{
    public void Configure(EntityTypeBuilder<PlannedMealPart> builder)
    {
        builder.ToTable("planned_meal_parts");

        builder.HasKey(part => part.Id);
        builder.Property(part => part.Id).ValueGeneratedNever();

        builder.Property(part => part.PlannedMealId).IsRequired();
        builder.Property(part => part.MemberProfileId).IsRequired();
        builder.Property(part => part.PortionMultiplier).IsRequired().HasPrecision(5, 2);

        // FK to PlannedMeal
        builder.HasOne<PlannedMeal>()
            .WithMany()
            .HasForeignKey(part => part.PlannedMealId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to MemberProfile
        builder.HasOne<LifeOS.Domain.Households.MemberProfile>()
            .WithMany()
            .HasForeignKey(part => part.MemberProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(part => part.PlannedMealId);
        builder.HasIndex(part => part.MemberProfileId);
    }
}
