using System.Net;
using System.Net.Http.Json;
using LifeOS.Api.Health;
using LifeOS.Application.Health.Contracts;

namespace LifeOS.Api.IntegrationTests;

/// <summary>ADR 0018: the symptom journal, its daily check-in, private to its account.</summary>
[Collection(PostgresCollection.Name)]
public sealed class HealthSymptomsTests(PostgresContainerFixture postgres)
{
    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    [Fact]
    public async Task Symptoms_are_noted_at_any_time_or_in_the_daily_checkin()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var journal = await Read<SymptomJournalDto>(await client.GetAsync("/api/health/symptoms"));
        Assert.Equal(10, journal.Types.Count); // the default list
        Assert.Empty(journal.Days);
        var belly = journal.Types.Single(t => t.Name == "Mal de ventre");
        var fog = journal.Types.Single(t => t.Name == "Flou mental");
        Assert.Equal("mental", fog.Kind);

        // Yesterday was calm; today a symptom, then the check-in adds another one.
        await Read<SymptomDayDto>(await client.PostAsJsonAsync("/api/health/symptoms/checkin", new CheckinRequest(journal.Today.AddDays(-1), true, null)));
        var entry = await Read<SymptomEntryDto>(await client.PostAsJsonAsync("/api/health/symptoms/entries",
            new SymptomEntryRequest(belly.Id, null, new TimeOnly(14, 10), 6, "Après le déjeuner.")));
        Assert.Equal(("Mal de ventre", 6), (entry.SymptomName, entry.Intensity));
        var day = await Read<SymptomDayDto>(await client.PostAsJsonAsync("/api/health/symptoms/checkin",
            new CheckinRequest(null, false, [new CheckinSymptomRequest(fog.Id, 4)])));
        Assert.Equal((false, 6, 2), (day.Calm, day.MaxIntensity!.Value, day.Entries.Count));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/health/symptoms/checkin", new CheckinRequest(null, true, null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/health/symptoms/entries",
            new SymptomEntryRequest(belly.Id, journal.Today.AddDays(1), null, 5, null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/health/symptoms/entries",
            new SymptomEntryRequest(belly.Id, null, null, 11, null))).StatusCode);

        // A symptom noted on a calm day makes it a day with symptoms.
        await Read<SymptomEntryDto>(await client.PostAsJsonAsync("/api/health/symptoms/entries", new SymptomEntryRequest(fog.Id, journal.Today.AddDays(-1), null, 2, null)));
        journal = await Read<SymptomJournalDto>(await client.GetAsync("/api/health/symptoms?days=7"));
        Assert.Equal([(journal.Today, false), (journal.Today.AddDays(-1), false)], journal.Days.Select(d => (d.Day, d.Calm)));

        var headache = await Read<SymptomTypeDto>(await client.PostAsJsonAsync("/api/health/symptoms/types", new SymptomTypeRequest("Maux de tête", "physique")));
        Assert.Equal(11, (await Read<SymptomJournalDto>(await client.GetAsync("/api/health/symptoms"))).Types.Count);
        Assert.True((await Read<SymptomTypeDto>(await client.PostAsync($"/api/health/symptoms/types/{headache.Id}/archive", null))).IsArchived);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/health/symptoms/entries", new SymptomEntryRequest(headache.Id, null, null, 3, null))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/health/symptoms/entries/{entry.Id}")).StatusCode);
    }

    [Fact]
    public async Task Another_account_cannot_read_or_use_the_symptoms()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        using var other = factory.CreateClient().AsUser(Guid.NewGuid());
        var journal = await Read<SymptomJournalDto>(await client.GetAsync("/api/health/symptoms"));
        var entry = await Read<SymptomEntryDto>(await client.PostAsJsonAsync("/api/health/symptoms/entries", new SymptomEntryRequest(journal.Types[0].Id, null, null, 5, null)));

        Assert.Empty((await Read<SymptomJournalDto>(await other.GetAsync("/api/health/symptoms"))).Days);
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync("/api/health/symptoms/entries", new SymptomEntryRequest(journal.Types[0].Id, null, null, 5, null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/health/symptoms/entries/{entry.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/health/symptoms/types/{journal.Types[0].Id}/archive", null)).StatusCode);
    }
}
