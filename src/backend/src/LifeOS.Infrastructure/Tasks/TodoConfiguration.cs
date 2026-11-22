using LifeOS.Domain.Households;
using LifeOS.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Tasks;

internal sealed class TodoConfiguration : IEntityTypeConfiguration<Todo>
{
    public void Configure(EntityTypeBuilder<Todo> builder)
    {
        builder.ToTable("todos", TasksSchema.Name, t =>
        {
            t.HasCheckConstraint("ck_todo_priority", "\"Priority\" IN ('high', 'normal', 'low')");
            t.HasCheckConstraint("ck_todo_state", "\"State\" IN ('open', 'done', 'abandoned')");
            t.HasCheckConstraint("ck_todo_done_on", "(\"State\" = 'done') = (\"DoneOn\" IS NOT NULL)");
            t.HasCheckConstraint("ck_todo_duration", $"\"DurationMinutes\" IS NULL OR (\"DurationMinutes\" >= 1 AND \"DurationMinutes\" <= {Todo.MaxDurationMinutes})");
            t.HasCheckConstraint("ck_todo_image", "(\"ImagePath\" IS NULL) = (\"ImageContentType\" IS NULL)");
        });
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.HasAlternateKey(t => new { t.Id, t.HouseholdId });
        builder.Property(t => t.Title).HasMaxLength(Todo.MaxTitleLength).IsRequired();
        builder.Property(t => t.Notes).HasMaxLength(Todo.MaxNotesLength);
        builder.Property(t => t.Priority).HasMaxLength(10).IsRequired();
        builder.Property(t => t.State).HasMaxLength(10).IsRequired();
        builder.Property(t => t.Tags).HasColumnName("tags").IsRequired();
        builder.Property(t => t.ImagePath).HasMaxLength(500);
        builder.Property(t => t.ImageContentType).HasMaxLength(100);
        builder.Ignore(t => t.HasImage);
        builder.Ignore(t => t.IsOpen);
        builder.Ignore(t => t.OrderedChecklist);
        builder.HasOne<Household>().WithMany().HasForeignKey(t => t.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MemberProfile>().WithMany().HasForeignKey(t => new { t.MemberProfileId, t.HouseholdId })
            .HasPrincipalKey(p => new { p.Id, p.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => new { t.HouseholdId, t.State, t.DueOn });
        // A todo's project is in the same household (nullable composite key: no project, no check).
        builder.HasOne<TaskProject>().WithMany().HasForeignKey(t => new { t.ProjectId, t.HouseholdId })
            .HasPrincipalKey(p => new { p.Id, p.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        builder.OwnsMany(t => t.Checklist, items =>
        {
            items.ToTable("todo_checklist_items", TasksSchema.Name);
            items.WithOwner().HasForeignKey("TodoId");
            items.HasKey(i => i.Id);
            items.Property(i => i.Id).ValueGeneratedNever();
            items.Property(i => i.Label).HasMaxLength(ChecklistItem.MaxLabelLength).IsRequired();
            items.HasIndex("TodoId", nameof(ChecklistItem.Position)).IsUnique();
        });
        builder.Navigation(t => t.Checklist).HasField("_checklist").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
