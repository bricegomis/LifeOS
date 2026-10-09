namespace LifeOS.Domain.FoodItems;

/// <summary>Preserves a historical purchase identifier without retaining a second product.</summary>
public sealed class ProductAlias
{
    public Guid ArticleId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid HouseholdId { get; private set; }
}
