using System.Net;
using System.Net.Http.Json;
using LifeOS.Api.Health;
using LifeOS.Application.Health.Contracts;

namespace LifeOS.Api.IntegrationTests;

/// <summary>ADR 0018: markers, analyses and body measures, private to their account.</summary>
[Collection(PostgresCollection.Name)]
public sealed class HealthAnalysesTests(PostgresContainerFixture postgres)
{
    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    [Fact]
    public async Task Analyses_give_each_marker_its_position_trend_and_next_measure()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var ferritin = await Read<MarkerDto>(await client.PostAsJsonAsync("/api/health/markers", new MarkerRequest("Ferritine", "µg/L", 30, 400, 80, 150, 6, ["Bilan fer"],
            "Réserves de fer.", "Carence en fer possible.", "Surcharge ou inflammation possible.")));
        Assert.Equal(("Réserves de fer.", "Carence en fer possible.", "Surcharge ou inflammation possible."), (ferritin.Description, ferritin.WhenLow, ferritin.WhenHigh));
        var homocysteine = await Read<MarkerDto>(await client.PostAsJsonAsync("/api/health/markers", new MarkerRequest("Homocystéine", "µmol/L", 5, 15, 5, 8, 0, ["Métabolisme"])));
        Assert.True(homocysteine.Due); // never measured: to do
        Assert.False(homocysteine.Late);

        var today = (await Read<MarkerListDto>(await client.GetAsync("/api/health/markers"))).Today;
        await Read<AnalysisDto>(await client.PostAsJsonAsync("/api/health/analyses", new AnalysisRequest(today.AddDays(-220), "Cerballiance", null, null,
            [new AnalysisValueRequest(ferritin.Id, 62)])));
        var analysis = await Read<AnalysisDto>(await client.PostAsJsonAsync("/api/health/analyses", new AnalysisRequest(today.AddDays(-40), "Cerballiance", null, null,
            [new AnalysisValueRequest(ferritin.Id, 88), new AnalysisValueRequest(homocysteine.Id, 9.5m)])));
        Assert.Equal(["in_target", "normal"], analysis.Values.Select(v => v.Status));

        var list = await Read<MarkerListDto>(await client.GetAsync("/api/health/markers"));
        var f = list.Markers.Single(m => m.Id == ferritin.Id);
        Assert.Equal(("in_target", "better", false, 2), (f.Status, f.Trend, f.Due, f.Values.Count));
        Assert.Equal(today.AddDays(-40).AddMonths(6), f.NextDueOn);
        Assert.False(list.Markers.Single(m => m.Id == homocysteine.Id).Due); // measured once, once is enough
        Assert.Equal(["Bilan fer", "Métabolisme"], list.Panels);
        Assert.Equal(2, (await Read<List<AnalysisDto>>(await client.GetAsync("/api/health/analyses"))).Count);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/health/analyses", new AnalysisRequest(null, null, null, null, []))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/health/analyses", new AnalysisRequest(null, null, null, Guid.NewGuid(),
            [new AnalysisValueRequest(ferritin.Id, 90)]))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/health/analyses/{analysis.Id}")).StatusCode);
    }

    [Fact]
    public async Task Body_measures_start_with_a_default_list()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var types = await Read<List<MeasureTypeDto>>(await client.GetAsync("/api/health/measures"));
        Assert.Equal(["Fréquence cardiaque au repos", "Poids", "Tour de taille"], types.Select(t => t.Name));
        var weight = types.Single(t => t.Name == "Poids");
        var updated = await Read<MeasureTypeDto>(await client.PostAsJsonAsync("/api/health/measures/values", new MeasurementRequest(weight.Id, null, 75.9m, "Le matin")));
        Assert.Equal(75.9m, Assert.Single(updated.Values).Value);
        Assert.Null(updated.Status); // no target yet
        var targeted = await Read<MeasureTypeDto>(await client.PutAsJsonAsync($"/api/health/measures/types/{weight.Id}", new MeasureTypeRequest("Poids", "kg", null, 74)));
        Assert.Equal(("above_target", (decimal?)74), (targeted.Status, targeted.TargetMax));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/health/measures/types/{weight.Id}", new MeasureTypeRequest("Poids", "kg", 80, 74))).StatusCode);
        var tension = await Read<MeasureTypeDto>(await client.PostAsJsonAsync("/api/health/measures/types", new MeasureTypeRequest("Tension systolique", "mmHg", null, 120)));
        Assert.Equal((decimal?)120, tension.TargetMax);
        Assert.Equal(4, (await Read<List<MeasureTypeDto>>(await client.GetAsync("/api/health/measures"))).Count);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/health/measures/values/{updated.Values[0].Id}")).StatusCode);
        Assert.True((await Read<MeasureTypeDto>(await client.PostAsync($"/api/health/measures/types/{tension.Id}/archive", null))).IsArchived);
    }

    [Fact]
    public async Task Another_account_sees_and_uses_none_of_it()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        using var other = factory.CreateClient().AsUser(Guid.NewGuid());
        var marker = await Read<MarkerDto>(await client.PostAsJsonAsync("/api/health/markers", new MarkerRequest("TSH", "mUI/L", 0.4m, 4, 1, 2, 12, null)));
        var analysis = await Read<AnalysisDto>(await client.PostAsJsonAsync("/api/health/analyses", new AnalysisRequest(null, null, null, null, [new AnalysisValueRequest(marker.Id, 2.4m)])));
        var weight = (await Read<List<MeasureTypeDto>>(await client.GetAsync("/api/health/measures"))).First();

        Assert.Empty((await Read<MarkerListDto>(await other.GetAsync("/api/health/markers?includeArchived=true"))).Markers);
        Assert.Empty(await Read<List<AnalysisDto>>(await other.GetAsync("/api/health/analyses")));
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/health/analyses/{analysis.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/health/markers/{marker.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync("/api/health/analyses", new AnalysisRequest(null, null, null, null, [new AnalysisValueRequest(marker.Id, 3)]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync("/api/health/measures/values", new MeasurementRequest(weight.Id, null, 80, null))).StatusCode);
    }
}
