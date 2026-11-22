using LifeOS.Domain.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Health;

internal sealed class SymptomTypeConfiguration : IEntityTypeConfiguration<SymptomType>
{
    public void Configure(EntityTypeBuilder<SymptomType> builder)
    {
        builder.ToTable("symptom_types", HealthSchema.Name, t => t.HasCheckConstraint("ck_symptom_type_kind", "\"Kind\" IN ('physique', 'mental')"));
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.HasAlternateKey(t => new { t.Id, t.OwnerUserId });
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Kind).HasMaxLength(10).IsRequired();
        HealthOwnership.Configure(builder);
    }
}

internal sealed class SymptomEntryConfiguration : IEntityTypeConfiguration<SymptomEntry>
{
    public void Configure(EntityTypeBuilder<SymptomEntry> builder)
    {
        builder.ToTable("symptom_entries", HealthSchema.Name, t => t.HasCheckConstraint("ck_symptom_entry_intensity", "\"Intensity\" >= 1 AND \"Intensity\" <= 10"));
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Note).HasMaxLength(SymptomEntry.MaxNoteLength);
        HealthOwnership.Configure(builder);
        // The symptom belongs to the same account.
        builder.HasOne<SymptomType>().WithMany().HasForeignKey(e => new { e.SymptomTypeId, e.OwnerUserId })
            .HasPrincipalKey(t => new { t.Id, t.OwnerUserId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.OwnerUserId, e.Day });
    }
}

internal sealed class CalmDayConfiguration : IEntityTypeConfiguration<CalmDay>
{
    public void Configure(EntityTypeBuilder<CalmDay> builder)
    {
        builder.ToTable("calm_days", HealthSchema.Name);
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        HealthOwnership.Configure(builder);
        builder.HasIndex(d => new { d.OwnerUserId, d.Day }).IsUnique();
    }
}
