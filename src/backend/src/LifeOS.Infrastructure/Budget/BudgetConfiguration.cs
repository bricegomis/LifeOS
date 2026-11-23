using LifeOS.Domain.Budget;
using LifeOS.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Budget;

/// <summary>The Budget module owns its PostgreSQL schema (ADR 0009, ADR 0020).</summary>
internal static class BudgetSchema
{
    public const string Name = "budget";
}

/// <summary>Budget rows belong to a household, an account and a profile, like health (ADR 0020).</summary>
internal static class BudgetOwnership
{
    public static void Configure<T>(EntityTypeBuilder<T> builder) where T : class
    {
        builder.HasOne<Household>().WithMany().HasForeignKey("HouseholdId").OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey("OwnerUserId").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberProfile>().WithMany().HasForeignKey("MemberProfileId", "HouseholdId")
            .HasPrincipalKey(nameof(MemberProfile.Id), nameof(MemberProfile.HouseholdId)).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex("HouseholdId", "OwnerUserId");
    }
}

internal sealed class BudgetLineConfiguration : IEntityTypeConfiguration<BudgetLine>
{
    public void Configure(EntityTypeBuilder<BudgetLine> builder)
    {
        builder.ToTable("lines", BudgetSchema.Name, t =>
        {
            t.HasCheckConstraint("ck_budget_line_kind", "\"Kind\" IN ('fixed', 'envelope', 'extra', 'off', 'income')");
            t.HasCheckConstraint("ck_budget_line_supplements", "NOT \"FromSupplements\" OR \"Kind\" IN ('fixed', 'envelope')");
        });
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.HasAlternateKey(l => new { l.Id, l.OwnerUserId });
        builder.Property(l => l.Name).HasMaxLength(BudgetLine.MaxNameLength).IsRequired();
        builder.Property(l => l.Category).HasMaxLength(BudgetLine.MaxCategoryLength).IsRequired();
        builder.Property(l => l.Kind).HasMaxLength(10).IsRequired();
        builder.Property(l => l.Notes).HasMaxLength(BudgetLine.MaxNotesLength);
        builder.Ignore(l => l.HasAmount);
        builder.Ignore(l => l.IsExpense);
        BudgetOwnership.Configure(builder);
        builder.OwnsMany(l => l.Amounts, amounts =>
        {
            amounts.ToTable("line_amounts", BudgetSchema.Name, t =>
            {
                t.HasCheckConstraint("ck_budget_amount_period", "\"Period\" IN ('month', 'year', 'months')");
                t.HasCheckConstraint("ck_budget_amount_values", "\"Amount\" >= 0 AND \"Months\" >= 1 AND \"Months\" <= 60");
            });
            amounts.WithOwner().HasForeignKey("LineId");
            amounts.HasKey("LineId", nameof(BudgetAmount.FromMonth));
            amounts.Property(a => a.Amount).HasPrecision(12, 2);
            amounts.Property(a => a.Period).HasMaxLength(10).IsRequired();
        });
        builder.Navigation(l => l.Amounts).HasField("_amounts").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class BudgetOperationConfiguration : IEntityTypeConfiguration<BudgetOperation>
{
    public void Configure(EntityTypeBuilder<BudgetOperation> builder)
    {
        builder.ToTable("operations", BudgetSchema.Name, t =>
        {
            t.HasCheckConstraint("ck_budget_operation_source", "\"Source\" IN ('ofx', 'manual')");
            t.HasCheckConstraint("ck_budget_operation_bank", "(\"Source\" = 'ofx') = (\"BankId\" IS NOT NULL AND \"Account\" IS NOT NULL)");
            t.HasCheckConstraint("ck_budget_operation_spread", "\"Spread\" >= 1 AND \"Spread\" <= 36");
            t.HasCheckConstraint("ck_budget_operation_classified", "(\"LineId\" IS NULL) = (\"ClassifiedBy\" IS NULL)");
            t.HasCheckConstraint("ck_budget_operation_by", "\"ClassifiedBy\" IS NULL OR \"ClassifiedBy\" IN ('rule', 'hand', 'assistant')");
        });
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Source).HasMaxLength(10).IsRequired();
        builder.Property(o => o.Account).HasMaxLength(BudgetOperation.MaxAccountLength);
        builder.Property(o => o.BankId).HasMaxLength(BudgetOperation.MaxBankIdLength);
        builder.Property(o => o.Label).HasMaxLength(BudgetOperation.MaxLabelLength).IsRequired();
        builder.Property(o => o.Amount).HasPrecision(12, 2);
        builder.Property(o => o.ClassifiedBy).HasMaxLength(10);
        builder.Ignore(o => o.Month);
        builder.Ignore(o => o.Share);
        BudgetOwnership.Configure(builder);
        // The line belongs to the same account; a line with operations cannot be deleted.
        builder.HasOne<BudgetLine>().WithMany().HasForeignKey(o => new { o.LineId, o.OwnerUserId })
            .HasPrincipalKey(l => new { l.Id, l.OwnerUserId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BudgetRule>().WithMany().HasForeignKey(o => o.RuleId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(o => new { o.OwnerUserId, o.Account, o.BankId }).IsUnique().HasFilter("\"BankId\" IS NOT NULL");
        builder.HasIndex(o => new { o.OwnerUserId, o.Date });
    }
}

internal sealed class BudgetRuleConfiguration : IEntityTypeConfiguration<BudgetRule>
{
    public void Configure(EntityTypeBuilder<BudgetRule> builder)
    {
        builder.ToTable("rules", BudgetSchema.Name, t =>
        {
            t.HasCheckConstraint("ck_budget_rule_direction", "\"Direction\" IN ('any', 'out', 'in')");
            t.HasCheckConstraint("ck_budget_rule_spread", "\"Spread\" >= 1 AND \"Spread\" <= 36");
        });
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Text).HasMaxLength(BudgetRule.MaxTextLength).IsRequired();
        builder.Property(r => r.Direction).HasMaxLength(5).IsRequired();
        BudgetOwnership.Configure(builder);
        builder.HasOne<BudgetLine>().WithMany().HasForeignKey(r => new { r.LineId, r.OwnerUserId })
            .HasPrincipalKey(l => new { l.Id, l.OwnerUserId }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BudgetAccountConfiguration : IEntityTypeConfiguration<BudgetAccount>
{
    public void Configure(EntityTypeBuilder<BudgetAccount> builder)
    {
        builder.ToTable("accounts", BudgetSchema.Name);
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Account).HasMaxLength(BudgetOperation.MaxAccountLength).IsRequired();
        builder.Property(a => a.Balance).HasPrecision(14, 2);
        BudgetOwnership.Configure(builder);
        builder.HasIndex(a => new { a.OwnerUserId, a.Account }).IsUnique();
    }
}
