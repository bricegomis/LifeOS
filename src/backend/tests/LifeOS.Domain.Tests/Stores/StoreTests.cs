using LifeOS.Domain.Stores;

namespace LifeOS.Domain.Tests.Stores;

public class StoreTests
{
    [Fact]
    public void Create_rejects_empty_household_id()
    {
        Assert.Throws<ArgumentException>(() => Store.Create(Guid.Empty, "Marché", "1 rue", true, true));
    }

    [Fact]
    public void Create_rejects_blank_name()
    {
        Assert.Throws<ArgumentException>(() => Store.Create(Guid.NewGuid(), "  ", "1 rue", true, true));
    }

    [Fact]
    public void Create_trims_name_and_address_and_stamps_household()
    {
        var householdId = Guid.NewGuid();

        var store = Store.Create(householdId, "  Marché  ", "  1 rue  ", isOrganic: true, isLocal: false);

        Assert.Equal(householdId, store.HouseholdId);
        Assert.Equal("Marché", store.Name);
        Assert.Equal("1 rue", store.Address);
        Assert.True(store.IsOrganic);
        Assert.False(store.IsLocal);
    }
}
