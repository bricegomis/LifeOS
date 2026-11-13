using System.Net;
using System.Net.Http.Json;
using LifeOS.Api.Sports;

namespace LifeOS.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class SportTemplatesTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Sport_templates_are_household_scoped_and_editable_by_their_household_only()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        using var other = factory.CreateClient().AsUser(Guid.NewGuid());
        var request = new SportTemplateRequest("Footing", "running", 40, 6, 350);
        var created = await client.PostAsJsonAsync("/api/sport-templates", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var template = (await created.Content.ReadFromJsonAsync<SportTemplateDto>())!;
        Assert.Equal("Course", template.SportLabel);

        Assert.Empty((await other.GetFromJsonAsync<List<SportTemplateDto>>("/api/sport-templates"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync(
            $"/api/sport-templates/{template.Id}", request with { Calories = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/sport-templates/{template.Id}")).StatusCode);

        var update = await client.PutAsJsonAsync($"/api/sport-templates/{template.Id}", request with { Calories = 450 });
        update.EnsureSuccessStatusCode();
        Assert.Equal(450, (await update.Content.ReadFromJsonAsync<SportTemplateDto>())!.Calories);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/sport-templates/{template.Id}")).StatusCode);
        Assert.True(Assert.Single((await client.GetFromJsonAsync<List<SportTemplateDto>>("/api/sport-templates"))!).IsArchived);
    }

    [Fact]
    public async Task A_template_sport_comes_from_the_fixed_list_and_only_endurance_sports_take_a_distance()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/sport-templates",
            new SportTemplateRequest("Footing", "run", 40, null, 350))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/sport-templates",
            new SportTemplateRequest("Muscu", "strength", 50, 3, 300))).StatusCode);
    }
}
