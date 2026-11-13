using LifeOS.Domain.Food.Foods;
using LifeOS.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Food.Foods;

internal sealed class FoodItemConfiguration : IEntityTypeConfiguration<FoodItem>
{
    public void Configure(EntityTypeBuilder<FoodItem> b)
    {
        b.ToTable("foods", FoodSchema.Name, t =>
        {
            t.HasCheckConstraint("ck_food_reference_unit", "reference_unit IN ('', '100 g', '100 ml', '1 pièce')");
            t.HasCheckConstraint("ck_food_fodmap", "fodmap IS NULL OR fodmap IN ('low', 'moderate', 'high')");
            t.HasCheckConstraint("ck_food_nova", "nova IS NULL OR nova BETWEEN 1 AND 4");
            t.HasCheckConstraint("ck_food_family", $"family IN ({SqlList(FoodClassification.Families.Keys)})");
            t.HasCheckConstraint("ck_food_roles", $"roles <@ ARRAY[{SqlList(FoodClassification.Roles.Keys)}]::text[]");
            t.HasCheckConstraint("ck_food_conservation",
                $"conservation IS NULL OR conservation IN ({SqlList(FoodClassification.Conservations.Keys)})");
            t.HasCheckConstraint("ck_food_season_months", "season_months <@ ARRAY[1,2,3,4,5,6,7,8,9,10,11,12]");
        });
        b.HasKey(f => f.Id);
        b.Property(f => f.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(f => f.HouseholdId).HasColumnName("household_id");
        b.HasAlternateKey(f => new { f.Id, f.HouseholdId });
        b.Property(f => f.Name).HasColumnName("name").HasMaxLength(FoodItem.MaxNameLength).IsRequired();
        b.Property(f => f.ReferenceUnit).HasColumnName("reference_unit").HasMaxLength(10).IsRequired();
        b.Property(f => f.Fodmap).HasColumnName("fodmap").HasMaxLength(10);
        b.Property(f => f.Nova).HasColumnName("nova");
        b.Property(f => f.DigestiveNotes).HasColumnName("digestive_notes").HasMaxLength(2000);
        b.Property(f => f.Family).HasColumnName("family").HasMaxLength(20).IsRequired();
        b.Property(f => f.Roles).HasColumnName("roles").IsRequired();
        b.Property(f => f.Conservation).HasColumnName("conservation").HasMaxLength(10);
        b.Property(f => f.SeasonMonths).HasColumnName("season_months").IsRequired();
        b.Property(f => f.Tags).HasColumnName("tags").IsRequired();
        b.Property(f => f.ImagePath).HasColumnName("image_path").HasMaxLength(500);
        b.Property(f => f.ImageContentType).HasColumnName("image_content_type").HasMaxLength(100);
        b.Property(f => f.IsArchived).HasColumnName("is_archived");
        b.Property(f => f.CreatedAt).HasColumnName("created_at");
        b.Property(f => f.UpdatedAt).HasColumnName("updated_at");
        b.Ignore(f => f.MissingFields);
        b.Ignore(f => f.HasImage);
        b.OwnsOne(f => f.Nutrition, n =>
        {
            n.Property(v => v.CaloriesPerUnit).HasColumnName("calories");
            n.Property(v => v.ProteinsPerUnit).HasColumnName("proteins");
            n.Property(v => v.CarbsPerUnit).HasColumnName("carbs");
            n.Property(v => v.FatsPerUnit).HasColumnName("fats");
            n.Property(v => v.FibersPerUnit).HasColumnName("fibers");
        });
        b.HasOne<Household>().WithMany().HasForeignKey(f => f.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(f => f.HouseholdId);
    }

    private static string SqlList(IEnumerable<string> codes) => string.Join(", ", codes.Select(c => $"'{c}'"));
}
