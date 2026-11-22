using LifeOS.Domain.Households;
using LifeOS.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Tasks;

internal sealed class TaskRoutineConfiguration : IEntityTypeConfiguration<TaskRoutine>
{
    public void Configure(EntityTypeBuilder<TaskRoutine> builder)
    {
        builder.ToTable("routines", TasksSchema.Name, t =>
        {
            t.HasCheckConstraint("ck_task_routine_priority", "\"Priority\" IN ('high', 'normal', 'low')");
            t.HasCheckConstraint("ck_task_routine_state", "\"State\" IN ('active', 'paused', 'stopped')");
            t.HasCheckConstraint("ck_task_routine_duration", $"\"DurationMinutes\" IS NULL OR (\"DurationMinutes\" >= 1 AND \"DurationMinutes\" <= {Todo.MaxDurationMinutes})");
            t.HasCheckConstraint("ck_task_routine_image", "(\"ImagePath\" IS NULL) = (\"ImageContentType\" IS NULL)");
            t.HasCheckConstraint("ck_task_routine_kind", "\"RecurrenceKind\" IN ('fixed', 'delay')");
            t.HasCheckConstraint("ck_task_routine_unit", "\"RecurrenceUnit\" IN ('day', 'week', 'month')");
            t.HasCheckConstraint("ck_task_routine_every", "\"RecurrenceEvery\" >= 1 AND \"RecurrenceEvery\" <= 365");
            t.HasCheckConstraint("ck_task_routine_days", "\"RecurrenceDays\" >= 0 AND \"RecurrenceDays\" <= 127");
            t.HasCheckConstraint("ck_task_routine_month_day", "\"RecurrenceMonthDay\" >= 1 AND \"RecurrenceMonthDay\" <= 31");
            t.HasCheckConstraint("ck_task_routine_delay", "\"RecurrenceDelayDays\" >= 1 AND \"RecurrenceDelayDays\" <= 365");
        });
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.HasAlternateKey(r => new { r.Id, r.HouseholdId });
        builder.Property(r => r.Title).HasMaxLength(Todo.MaxTitleLength).IsRequired();
        builder.Property(r => r.Notes).HasMaxLength(Todo.MaxNotesLength);
        builder.Property(r => r.Priority).HasMaxLength(10).IsRequired();
        builder.Property(r => r.State).HasMaxLength(10).IsRequired();
        builder.Property(r => r.Tags).HasColumnName("tags").IsRequired();
        builder.Property(r => r.ImagePath).HasMaxLength(500);
        builder.Property(r => r.ImageContentType).HasMaxLength(100);
        builder.Ignore(r => r.HasImage);
        builder.Ignore(r => r.IsActive);
        builder.ComplexProperty(r => r.Recurrence, recurrence =>
        {
            recurrence.Property(x => x.Kind).HasColumnName("RecurrenceKind").HasMaxLength(10).IsRequired();
            recurrence.Property(x => x.Unit).HasColumnName("RecurrenceUnit").HasMaxLength(10).IsRequired();
            recurrence.Property(x => x.Every).HasColumnName("RecurrenceEvery");
            recurrence.Property(x => x.Days).HasColumnName("RecurrenceDays");
            recurrence.Property(x => x.MonthDay).HasColumnName("RecurrenceMonthDay");
            recurrence.Property(x => x.DelayDays).HasColumnName("RecurrenceDelayDays");
        });
        builder.HasOne<Household>().WithMany().HasForeignKey(r => r.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MemberProfile>().WithMany().HasForeignKey(r => new { r.MemberProfileId, r.HouseholdId })
            .HasPrincipalKey(p => new { p.Id, p.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.HouseholdId, r.State });
        builder.OwnsMany(r => r.Passes, passes =>
        {
            passes.ToTable("routine_passes", TasksSchema.Name, t =>
                t.HasCheckConstraint("ck_routine_pass_kind", "\"Kind\" IN ('done', 'skipped')"));
            passes.WithOwner().HasForeignKey("RoutineId");
            passes.HasKey(p => p.Id);
            passes.Property(p => p.Id).ValueGeneratedNever();
            passes.Property(p => p.Kind).HasMaxLength(10).IsRequired();
            passes.Property(p => p.Note).HasMaxLength(TaskRoutine.MaxNoteLength);
            passes.Ignore(p => p.OnTime);
            passes.HasIndex("RoutineId", nameof(RoutinePass.DueOn));
        });
        builder.Navigation(r => r.Passes).HasField("_passes").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
