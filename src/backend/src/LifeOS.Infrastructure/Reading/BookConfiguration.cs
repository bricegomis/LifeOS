using LifeOS.Domain.Households;
using LifeOS.Domain.Reading;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Reading;

internal sealed class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("books", ReadingSchema.Name, t =>
        {
            t.HasCheckConstraint("ck_book_format", "\"Format\" IN ('paper', 'ereader', 'audio', 'comic')");
            t.HasCheckConstraint("ck_book_tome", "\"Tome\" IS NULL OR (\"SeriesId\" IS NOT NULL AND \"Tome\" > 0)");
            t.HasCheckConstraint("ck_book_image", "(\"ImagePath\" IS NULL) = (\"ImageContentType\" IS NULL)");
        });
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.HasAlternateKey(b => new { b.Id, b.HouseholdId });
        builder.Property(b => b.Title).HasMaxLength(Book.MaxTitleLength).IsRequired();
        builder.Property(b => b.Authors).HasMaxLength(Book.MaxAuthorsLength).IsRequired();
        builder.Property(b => b.Format).HasMaxLength(10).IsRequired();
        builder.Property(b => b.Genres).HasColumnName("genres").IsRequired();
        builder.Property(b => b.Tome).HasPrecision(6, 2);
        builder.Property(b => b.Notes).HasMaxLength(Book.MaxNotesLength);
        builder.Property(b => b.NotWantedReason).HasMaxLength(Book.MaxReasonLength);
        builder.Property(b => b.ImagePath).HasMaxLength(500);
        builder.Property(b => b.ImageContentType).HasMaxLength(100);
        builder.Ignore(b => b.HasImage);
        builder.Ignore(b => b.State);
        builder.Ignore(b => b.LastRating);
        builder.Ignore(b => b.LastReading);
        builder.Ignore(b => b.CurrentReading);
        builder.Ignore(b => b.OrderedReadings);
        builder.Ignore(b => b.OrderedQuotes);
        builder.HasOne<Household>().WithMany().HasForeignKey(b => b.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MemberProfile>().WithMany().HasForeignKey(b => new { b.MemberProfileId, b.HouseholdId })
            .HasPrincipalKey(p => new { p.Id, p.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        // A book's series is in the same household (nullable composite key: no series, no check).
        builder.HasOne<BookSeries>().WithMany().HasForeignKey(b => new { b.SeriesId, b.HouseholdId })
            .HasPrincipalKey(s => new { s.Id, s.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(b => new { b.HouseholdId, b.Title });
        builder.OwnsMany(b => b.Readings, readings =>
        {
            readings.ToTable("book_readings", ReadingSchema.Name, t =>
            {
                t.HasCheckConstraint("ck_book_reading_outcome", "\"Outcome\" IN ('reading', 'read', 'stopped')");
                t.HasCheckConstraint("ck_book_reading_rating", "\"Rating\" IS NULL OR (\"Rating\" >= 0 AND \"Rating\" <= 10)");
                t.HasCheckConstraint("ck_book_reading_dates", "\"StartedOn\" IS NULL OR \"FinishedOn\" IS NULL OR \"FinishedOn\" >= \"StartedOn\"");
            });
            readings.WithOwner().HasForeignKey("BookId");
            readings.HasKey(r => r.Id);
            readings.Property(r => r.Id).ValueGeneratedNever();
            readings.Property(r => r.Outcome).HasMaxLength(10).IsRequired();
            readings.Property(r => r.Review).HasMaxLength(BookReading.MaxReviewLength);
            readings.Property(r => r.StopReason).HasMaxLength(Book.MaxReasonLength);
            readings.HasIndex("BookId", nameof(BookReading.Position)).IsUnique();
            // One reading in progress at a time per book.
            readings.HasIndex("BookId").IsUnique().HasFilter("\"Outcome\" = 'reading'").HasDatabaseName("ix_book_readings_one_in_progress");
        });
        builder.Navigation(b => b.Readings).HasField("_readings").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.OwnsMany(b => b.Quotes, quotes =>
        {
            quotes.ToTable("book_quotes", ReadingSchema.Name);
            quotes.WithOwner().HasForeignKey("BookId");
            quotes.HasKey(q => q.Id);
            quotes.Property(q => q.Id).ValueGeneratedNever();
            quotes.Property(q => q.Text).HasMaxLength(BookQuote.MaxTextLength).IsRequired();
            quotes.Property(q => q.Place).HasMaxLength(BookQuote.MaxPlaceLength);
            quotes.HasIndex("BookId", nameof(BookQuote.Position)).IsUnique();
        });
        builder.Navigation(b => b.Quotes).HasField("_quotes").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class BookSeriesConfiguration : IEntityTypeConfiguration<BookSeries>
{
    public void Configure(EntityTypeBuilder<BookSeries> builder)
    {
        builder.ToTable("series", ReadingSchema.Name, t =>
            t.HasCheckConstraint("ck_book_series_tomes", $"\"TotalTomes\" IS NULL OR (\"TotalTomes\" >= 1 AND \"TotalTomes\" <= {BookSeries.MaxTomes})"));
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.HasAlternateKey(s => new { s.Id, s.HouseholdId });
        builder.Property(s => s.Name).HasMaxLength(BookSeries.MaxNameLength).IsRequired();
        builder.HasOne<Household>().WithMany().HasForeignKey(s => s.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(s => new { s.HouseholdId, s.Name }).IsUnique();
    }
}
