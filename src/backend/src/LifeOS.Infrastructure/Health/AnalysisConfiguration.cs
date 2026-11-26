using LifeOS.Domain.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Health;

internal sealed class MarkerConfiguration : IEntityTypeConfiguration<Marker>
{
    public void Configure(EntityTypeBuilder<Marker> builder)
    {
        builder.ToTable("markers", HealthSchema.Name, t => t.HasCheckConstraint("ck_marker_frequency", "\"FrequencyMonths\" >= 0 AND \"FrequencyMonths\" <= 120"));
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.HasAlternateKey(m => new { m.Id, m.OwnerUserId });
        builder.Property(m => m.Name).HasMaxLength(100).IsRequired();
        builder.Property(m => m.Unit).HasMaxLength(30).IsRequired();
        foreach (var p in new[] { nameof(Marker.LabMin), nameof(Marker.LabMax), nameof(Marker.TargetMin), nameof(Marker.TargetMax) })
            builder.Property(p).HasPrecision(14, 4);
        builder.Property(m => m.Panels).HasColumnName("panels").IsRequired();
        builder.Property(m => m.Description).HasMaxLength(Marker.MaxTextLength);
        builder.Property(m => m.WhenLow).HasMaxLength(Marker.MaxTextLength);
        builder.Property(m => m.WhenHigh).HasMaxLength(Marker.MaxTextLength);
        HealthOwnership.Configure(builder);
    }
}

internal sealed class AnalysisConfiguration : IEntityTypeConfiguration<Analysis>
{
    public void Configure(EntityTypeBuilder<Analysis> builder)
    {
        builder.ToTable("analyses", HealthSchema.Name);
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Laboratory).HasMaxLength(100);
        builder.Property(a => a.Notes).HasMaxLength(Analysis.MaxNotesLength);
        HealthOwnership.Configure(builder);
        // The report it was read from: a document of the same account (checked by the library).
        builder.HasOne<HealthDocument>().WithMany().HasForeignKey(a => a.DocumentId).OnDelete(DeleteBehavior.SetNull);
        builder.OwnsMany(a => a.Values, values =>
        {
            values.ToTable("analysis_values", HealthSchema.Name);
            values.WithOwner().HasForeignKey("AnalysisId");
            values.HasKey("AnalysisId", nameof(AnalysisValue.MarkerId));
            values.Property(v => v.Value).HasPrecision(14, 4);
            values.HasOne<Marker>().WithMany().HasForeignKey(v => v.MarkerId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Navigation(a => a.Values).HasField("_values").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class MeasureTypeConfiguration : IEntityTypeConfiguration<MeasureType>
{
    public void Configure(EntityTypeBuilder<MeasureType> builder)
    {
        builder.ToTable("measure_types", HealthSchema.Name);
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.HasAlternateKey(t => new { t.Id, t.OwnerUserId });
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Unit).HasMaxLength(30).IsRequired();
        HealthOwnership.Configure(builder);
    }
}

internal sealed class MeasurementConfiguration : IEntityTypeConfiguration<Measurement>
{
    public void Configure(EntityTypeBuilder<Measurement> builder)
    {
        builder.ToTable("measurements", HealthSchema.Name);
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Value).HasPrecision(14, 4);
        builder.Property(m => m.Note).HasMaxLength(500);
        HealthOwnership.Configure(builder);
        builder.HasOne<MeasureType>().WithMany().HasForeignKey(m => new { m.MeasureTypeId, m.OwnerUserId })
            .HasPrincipalKey(t => new { t.Id, t.OwnerUserId }).OnDelete(DeleteBehavior.Restrict);
    }
}
