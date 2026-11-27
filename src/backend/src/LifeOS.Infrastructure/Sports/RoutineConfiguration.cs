using LifeOS.Domain.Households;
using LifeOS.Domain.Sports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Sports;

internal sealed class ExerciseConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.ToTable("exercises", SportsSchema.Name, t =>
        {
            t.HasCheckConstraint("ck_exercise_measure", "\"Measure\" IN ('hold', 'reps')");
            t.HasCheckConstraint("ck_exercise_image", "(\"ImagePath\" IS NULL) = (\"ImageContentType\" IS NULL)");
            t.HasCheckConstraint("ck_exercise_default_load", "\"DefaultLoadKg\" IS NULL OR (\"DefaultLoadKg\" > 0 AND \"DefaultLoadKg\" <= 500)");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.HasAlternateKey(e => new { e.Id, e.HouseholdId });
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Instructions).HasMaxLength(Exercise.MaxInstructionsLength);
        builder.Property(e => e.Measure).HasMaxLength(10).IsRequired();
        builder.Property(e => e.DefaultLoadKg).HasPrecision(5, 1);
        builder.Property(e => e.ImagePath).HasMaxLength(500);
        builder.Property(e => e.ImageContentType).HasMaxLength(100);
        builder.Ignore(e => e.HasImage);
        builder.HasOne<Household>().WithMany().HasForeignKey(e => e.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.HouseholdId);
    }
}

internal sealed class RoutineConfiguration : IEntityTypeConfiguration<Routine>
{
    public void Configure(EntityTypeBuilder<Routine> builder)
    {
        builder.ToTable("routines", SportsSchema.Name, t =>
            t.HasCheckConstraint("ck_routine_period", "\"StoppedOn\" IS NULL OR \"StoppedOn\" >= \"StartedOn\""));
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.HasAlternateKey(r => new { r.Id, r.HouseholdId });
        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.Ignore(r => r.IsActive);
        builder.HasOne<Household>().WithMany().HasForeignKey(r => r.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MemberProfile>().WithMany().HasForeignKey(r => new { r.MemberProfileId, r.HouseholdId })
            .HasPrincipalKey(p => new { p.Id, p.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.HouseholdId, r.MemberProfileId });
        builder.OwnsMany(r => r.Versions, versions =>
        {
            versions.ToTable("routine_versions", SportsSchema.Name, t =>
            {
                t.HasCheckConstraint("ck_routine_version_days", "\"Days\" >= 1 AND \"Days\" <= 127");
                t.HasCheckConstraint("ck_routine_version_jokers", "\"JokersPerWeek\" >= 0 AND \"JokersPerWeek\" <= 2");
            });
            versions.WithOwner().HasForeignKey("RoutineId");
            versions.HasKey("RoutineId", nameof(RoutineVersion.ValidFrom));
            versions.Ignore(v => v.OrderedLines);
            versions.OwnsMany(v => v.Lines, lines =>
            {
                lines.ToTable("routine_lines", SportsSchema.Name, t =>
                {
                    t.HasCheckConstraint("ck_routine_line_sets", "\"Sets\" >= 1 AND \"Sets\" <= 20");
                    t.HasCheckConstraint("ck_routine_line_target", "\"Target\" >= 1 AND \"Target\" <= 10000");
                    t.HasCheckConstraint("ck_routine_line_load", "\"LoadKg\" IS NULL OR (\"LoadKg\" >= 0 AND \"LoadKg\" <= 500)");
                });
                lines.WithOwner().HasForeignKey("RoutineId", "ValidFrom");
                lines.HasKey("RoutineId", "ValidFrom", nameof(RoutineLine.ExerciseId));
                lines.Property(l => l.LoadKg).HasPrecision(5, 1);
                lines.HasOne<Exercise>().WithMany().HasForeignKey(l => new { l.ExerciseId, l.HouseholdId })
                    .HasPrincipalKey(e => new { e.Id, e.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
            });
            versions.Navigation(v => v.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Navigation(r => r.Versions).HasField("_versions").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class RoutineSessionConfiguration : IEntityTypeConfiguration<RoutineSession>
{
    public void Configure(EntityTypeBuilder<RoutineSession> builder)
    {
        builder.ToTable("routine_sessions", SportsSchema.Name);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.HasAlternateKey(s => new { s.Id, s.HouseholdId });
        builder.Property(s => s.Comment).HasMaxLength(RoutineSession.MaxCommentLength);
        builder.HasOne<Routine>().WithMany().HasForeignKey(s => new { s.RoutineId, s.HouseholdId })
            .HasPrincipalKey(r => new { r.Id, r.HouseholdId }).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(s => new { s.RoutineId, s.Date });
    }
}

internal sealed class ExerciseCompletionConfiguration : IEntityTypeConfiguration<ExerciseCompletion>
{
    public void Configure(EntityTypeBuilder<ExerciseCompletion> builder)
    {
        builder.ToTable("exercise_completions", SportsSchema.Name, t =>
        {
            t.HasCheckConstraint("ck_exercise_completion_value", "\"Value\" >= 1 AND \"Value\" <= 10000");
            t.HasCheckConstraint("ck_exercise_completion_load", "\"LoadKg\" IS NULL OR (\"LoadKg\" >= 0 AND \"LoadKg\" <= 500)");
            t.HasCheckConstraint("ck_exercise_completion_sets", "\"SetsDone\" >= 1 AND \"SetsDone\" <= 20");
        });
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.LoadKg).HasPrecision(5, 1);
        builder.HasOne<Routine>().WithMany().HasForeignKey(c => new { c.RoutineId, c.HouseholdId })
            .HasPrincipalKey(r => new { r.Id, r.HouseholdId }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<RoutineSession>().WithMany().HasForeignKey(c => new { c.SessionId, c.HouseholdId })
            .HasPrincipalKey(s => new { s.Id, s.HouseholdId }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Exercise>().WithMany().HasForeignKey(c => new { c.ExerciseId, c.HouseholdId })
            .HasPrincipalKey(e => new { e.Id, e.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.SessionId, c.ExerciseId }).IsUnique();
        builder.HasIndex(c => new { c.RoutineId, c.Date });
        builder.HasIndex(c => new { c.HouseholdId, c.ExerciseId });
    }
}
