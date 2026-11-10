using System.Net;
using System.Net.Http.Json;

namespace LifeOS.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class SportTemplatesTests(PostgresContainerFixture postgres)
{
    private sealed record TemplateDto(Guid Id, string Name, decimal Calories, bool IsArchived);

    [Fact]
    public async Task Sport_templates_are_household_scoped_and_editable_by_their_household_only()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        using var other = factory.CreateClient().AsUser(Guid.NewGuid());
        var request = new { name = "Footing", sport = "running", durationMinutes = 40, distanceKm = 6, intensity = "moderate", calories = 350 };
        var created = await client.PostAsJsonAsync("/api/sport-templates", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var template = (await created.Content.ReadFromJsonAsync<TemplateDto>())!;

        Assert.Empty((await other.GetFromJsonAsync<List<TemplateDto>>("/api/sport-templates"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync(
            $"/api/sport-templates/{template.Id}", request with { calories = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/sport-templates/{template.Id}")).StatusCode);

        var update = await client.PutAsJsonAsync($"/api/sport-templates/{template.Id}", request with { calories = 450 });
        update.EnsureSuccessStatusCode();
        Assert.Equal(450, (await update.Content.ReadFromJsonAsync<TemplateDto>())!.Calories);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/sport-templates/{template.Id}")).StatusCode);
        Assert.True(Assert.Single((await client.GetFromJsonAsync<List<TemplateDto>>("/api/sport-templates"))!).IsArchived);
    }
}
