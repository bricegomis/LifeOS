using LifeOS.Domain.FoodItems;
using LifeOS.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

public sealed class FoodItemConfiguration : IEntityTypeConfiguration<FoodItem>
{
    public void Configure(EntityTypeBuilder<FoodItem> b)
    {
        b.ToTable("foods");
        b.HasKey(f => f.Id);
        b.Property(f => f.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(f => f.HouseholdId).HasColumnName("household_id");
        b.HasAlternateKey(f => new { f.Id, f.HouseholdId });
        b.Property(f => f.Name).HasColumnName("name").HasMaxLength(500);
        b.Property(f => f.ReferenceUnit).HasColumnName("reference_unit").HasMaxLength(50);
        b.Property(f => f.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(20);
        b.Property(f => f.OffBarcode).HasColumnName("off_barcode").HasMaxLength(50);
        b.Property(f => f.OffPayload).HasColumnName("off_payload").HasColumnType("jsonb");
        b.Property(f => f.IsCorrectionOf).HasColumnName("is_correction_of");
        b.Property(f => f.CreatedAt).HasColumnName("created_at");
        b.Property(f => f.UpdatedAt).HasColumnName("updated_at");
        b.Property(f => f.Fodmap).HasMaxLength(20);
        b.Property(f => f.DigestiveNotes).HasMaxLength(2000);
        b.Ignore(f => f.MissingFields);
        b.OwnsOne(f => f.Nutrition, n =>
        {
            n.Property(v => v.CaloriesPerUnit).HasColumnName("calories_per_unit");
            n.Property(v => v.ProteinsPerUnit).HasColumnName("proteins_per_unit");
            n.Property(v => v.CarbsPerUnit).HasColumnName("carbs_per_unit");
            n.Property(v => v.FatsPerUnit).HasColumnName("fats_per_unit");
        });
        b.HasOne<Household>().WithMany().HasForeignKey(f => f.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FoodItem>().WithMany().HasForeignKey(f => f.IsCorrectionOf).OnDelete(DeleteBehavior.SetNull);
    }
}
