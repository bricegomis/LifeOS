using LifeOS.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Households;

internal sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("users", "households");

        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();
        builder.Property(user => user.GoogleSubject).HasMaxLength(255).IsRequired();
        builder.HasIndex(user => user.GoogleSubject).IsUnique();
        builder.Property(user => user.Email).HasMaxLength(UserAccount.MaxEmailLength).IsRequired();
        builder.Property(user => user.DisplayName).HasMaxLength(UserAccount.MaxDisplayNameLength);
        builder.Property(user => user.CreatedAt).IsRequired();
        builder.Property(user => user.LastSignInAt).IsRequired();
    }
}
