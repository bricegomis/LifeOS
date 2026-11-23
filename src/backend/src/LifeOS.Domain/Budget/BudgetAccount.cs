using LifeOS.Domain.Common;

namespace LifeOS.Domain.Budget;

/// <summary>A bank account seen in imported statements (ADR 0020): its last balance and last import, for display only.</summary>
public sealed class BudgetAccount : Entity
{
    public Guid HouseholdId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public string Account { get; private set; } = "";
    public decimal? Balance { get; private set; }
    public DateOnly? BalanceOn { get; private set; }
    public DateTimeOffset LastImportAt { get; private set; }
    public DateOnly? LastFrom { get; private set; }
    public DateOnly? LastTo { get; private set; }

    private BudgetAccount() { }

    public static BudgetAccount Create(Guid householdId, Guid ownerUserId, Guid memberProfileId, string account) =>
        new() { Id = Guid.NewGuid(), HouseholdId = householdId, OwnerUserId = ownerUserId, MemberProfileId = memberProfileId, Account = account.Trim() };

    /// <summary>Keeps the newest balance; an older statement imported later does not replace it.</summary>
    public void Imported(OfxStatement statement, DateTimeOffset now)
    {
        LastImportAt = now;
        LastFrom = statement.From;
        LastTo = statement.To;
        if (statement.Balance is { } balance && (BalanceOn is null || statement.BalanceOn is null || statement.BalanceOn >= BalanceOn))
        {
            Balance = balance;
            BalanceOn = statement.BalanceOn;
        }
    }
}
