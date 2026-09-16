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

    [Fact]
    public void UpdateDetails_trims_fields_and_updates_flags()
    {
        var store = Store.Create(Guid.NewGuid(), "Marché", "1 rue", isOrganic: true, isLocal: false);

        store.UpdateDetails("  Supermarché  ", "  2 rue  ", isOrganic: false, isLocal: true);

        Assert.Equal("Supermarché", store.Name);
        Assert.Equal("2 rue", store.Address);
        Assert.False(store.IsOrganic);
        Assert.True(store.IsLocal);
    }
}
