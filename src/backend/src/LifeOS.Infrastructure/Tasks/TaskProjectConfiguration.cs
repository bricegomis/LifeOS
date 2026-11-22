using LifeOS.Domain.Households;
using LifeOS.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Tasks;

internal sealed class TaskProjectConfiguration : IEntityTypeConfiguration<TaskProject>
{
    public void Configure(EntityTypeBuilder<TaskProject> builder)
    {
        builder.ToTable("projects", TasksSchema.Name, t =>
        {
            t.HasCheckConstraint("ck_task_project_state", "\"State\" IN ('active', 'done', 'abandoned')");
            t.HasCheckConstraint("ck_task_project_image", "(\"ImagePath\" IS NULL) = (\"ImageContentType\" IS NULL)");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.HasAlternateKey(p => new { p.Id, p.HouseholdId });
        builder.Property(p => p.Title).HasMaxLength(Todo.MaxTitleLength).IsRequired();
        builder.Property(p => p.Notes).HasMaxLength(Todo.MaxNotesLength);
        builder.Property(p => p.State).HasMaxLength(10).IsRequired();
        builder.Property(p => p.ImagePath).HasMaxLength(500);
        builder.Property(p => p.ImageContentType).HasMaxLength(100);
        builder.Ignore(p => p.HasImage);
        builder.Ignore(p => p.IsActive);
        builder.HasOne<Household>().WithMany().HasForeignKey(p => p.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(p => new { p.HouseholdId, p.State });
    }
}
