using LifeOS.Domain.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Health;

internal sealed class HealthDocumentConfiguration : IEntityTypeConfiguration<HealthDocument>
{
    public void Configure(EntityTypeBuilder<HealthDocument> builder)
    {
        builder.ToTable("documents", HealthSchema.Name, t =>
            t.HasCheckConstraint("ck_health_document_type", "\"Type\" IN ('ordonnance', 'compte_rendu', 'resultat', 'bilan', 'autre')"));
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Type).HasMaxLength(20).IsRequired();
        builder.Property(d => d.Title).HasMaxLength(200).IsRequired();
        builder.Property(d => d.Who).HasMaxLength(200);
        builder.Property(d => d.Notes).HasMaxLength(HealthDocument.MaxNotesLength);
        builder.Ignore(d => d.OrderedFiles);
        HealthOwnership.Configure(builder);
        builder.OwnsMany(d => d.Files, files =>
        {
            files.ToTable("document_files", HealthSchema.Name);
            files.WithOwner().HasForeignKey("DocumentId");
            files.HasKey(f => f.Id);
            files.Property(f => f.Id).ValueGeneratedNever();
            files.Property(f => f.StoredName).HasMaxLength(100).IsRequired();
            files.Property(f => f.ContentType).HasMaxLength(50).IsRequired();
            files.Property(f => f.OriginalName).HasMaxLength(200);
        });
        builder.Navigation(d => d.Files).HasField("_files").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments", HealthSchema.Name);
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Who).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Reason).HasMaxLength(300);
        builder.Property(a => a.Place).HasMaxLength(300);
        builder.Property(a => a.Notes).HasMaxLength(2000);
        builder.Property(a => a.BookingUrl).HasMaxLength(1000);
        HealthOwnership.Configure(builder);
        // The report is a document of the same account (checked by the library); removing a document unlinks it.
        builder.HasOne<HealthDocument>().WithMany().HasForeignKey(a => a.ReportDocumentId).OnDelete(DeleteBehavior.SetNull);
    }
}
