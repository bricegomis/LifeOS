using System.Net;
using System.Net.Http.Json;
using LifeOS.Api.Contracts;
using LifeOS.Application.Households;
using LifeOS.Application.Stores;

namespace LifeOS.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class BackendTaskIsolationTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Stores_support_household_scoped_crud()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var clientA = factory.CreateClient().AsUser(Guid.NewGuid());
        using var clientB = factory.CreateClient().AsUser(Guid.NewGuid());

        var createResponse = await clientA.PostAsJsonAsync(
            "/api/stores",
            new StoreRequest("Marché A", "1 rue A", IsOrganic: true, IsLocal: true));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<StoreDto>();
        Assert.NotNull(created);

        var updateResponse = await clientA.PutAsJsonAsync(
            $"/api/stores/{created.Id}",
            new StoreRequest("Marché A+", "2 rue A", IsOrganic: false, IsLocal: true));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<StoreDto>();
        Assert.NotNull(updated);
        Assert.Equal("Marché A+", updated.Name);
        Assert.Equal("2 rue A", updated.Address);
        Assert.False(updated.IsOrganic);

        Assert.Equal(HttpStatusCode.NotFound, (await clientB.GetAsync($"/api/stores/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.PutAsJsonAsync(
            $"/api/stores/{created.Id}",
            new StoreRequest("Intrusion", "N/A", IsOrganic: false, IsLocal: false))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.DeleteAsync($"/api/stores/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await clientA.DeleteAsync($"/api/stores/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Household_members_endpoint_returns_current_household_members_and_profiles()
    {
        var userId = Guid.NewGuid();
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(userId);

        var response = await client.GetAsync("/api/households/members");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var household = await response.Content.ReadFromJsonAsync<HouseholdMembersResponse>();
        Assert.NotNull(household);
        Assert.Single(household.Members);
        Assert.Single(household.MemberProfiles);
        Assert.Equal(userId, household.Members[0].SupabaseUserId);
        Assert.Equal("owner", household.Members[0].Role);
        Assert.Equal("Moi", household.MemberProfiles[0].DisplayName);
    }

    private sealed record HouseholdMembersResponse(
        Guid Id,
        List<HouseholdMemberDto> Members,
        List<MemberProfileDto> MemberProfiles);
}
