using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LifeOS.Api.Health;
using LifeOS.Application.Health.Contracts;
using LifeOS.Domain.Households;
using LifeOS.Infrastructure.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LifeOS.Api.IntegrationTests;

/// <summary>ADR 0018: supplements, their daily ticks and monthly cost, private to their account.</summary>
[Collection(PostgresCollection.Name)]
public sealed class HealthSupplementsTests(PostgresContainerFixture postgres)
{
    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static SupplementRequest Magnesium(SupplementPeriodRequest? period = null) => new("Magnésium bisglycinate", null, 2, "gelule", "soir", 180, 29.9m,
        "Pharmacie du centre", [new SupplementLinkRequest("Pharmacie", "https://www.example.fr/magnesium")], period ?? new SupplementPeriodRequest("year", null, null, null, null, null));

    [Fact]
    public async Task A_supplement_is_ticked_once_a_day_and_its_monthly_cost_computed()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var created = await client.PostAsJsonAsync("/api/health/supplements", Magnesium());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var supplement = await Read<SupplementDto>(created);
        Assert.Equal(9.97m, supplement.MonthlyCost);
        Assert.Equal(("Pharmacie", true, false), (supplement.Links[0].Label, supplement.Status.Active, supplement.TakenToday));

        supplement = await Read<SupplementDto>(await client.PostAsJsonAsync($"/api/health/supplements/{supplement.Id}/intakes", new SupplementIntakeRequest(null)));
        Assert.Equal((true, 1), (supplement.TakenToday, supplement.Streak));
        supplement = await Read<SupplementDto>(await client.PostAsJsonAsync($"/api/health/supplements/{supplement.Id}/intakes", new SupplementIntakeRequest(null, false)));
        Assert.False(supplement.TakenToday);

        var cure = await Read<SupplementDto>(await client.PostAsJsonAsync("/api/health/supplements",
            Magnesium(new SupplementPeriodRequest("cure", null, 3, "month", 1, "month")) with { Name = "Vitamine D" }));
        Assert.Equal((7.48m, 1, 90), (cure.MonthlyCost!.Value, cure.Status.DayInCure!.Value, cure.Status.CureDays!.Value));
        var list = await Read<SupplementListDto>(await client.GetAsync("/api/health/supplements"));
        Assert.Equal(17.45m, list.MonthlyTotal);

        await Read<SupplementDto>(await client.PostAsync($"/api/health/supplements/{cure.Id}/stop", null));
        list = await Read<SupplementListDto>(await client.GetAsync("/api/health/supplements"));
        Assert.Single(list.Supplements);
        Assert.Equal(2, (await Read<SupplementListDto>(await client.GetAsync("/api/health/supplements?includeStopped=true"))).Supplements.Count);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/health/supplements", Magnesium() with { Unit = "pilule" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/health/supplements",
            Magnesium() with { Links = [new SupplementLinkRequest(null, "http://example.fr")] })).StatusCode);
    }

    [Fact]
    public async Task Supplement_images_are_encrypted_on_disk()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var storage = factory.Services.GetRequiredService<IOptions<HealthFileStorageOptions>>().Value.StoragePath;
        var supplement = await Read<SupplementDto>(await client.PostAsJsonAsync("/api/health/supplements", Magnesium()));
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];
        var form = new MultipartFormDataContent();
        var image = new ByteArrayContent(png);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(image, "image", "pot.png");
        Assert.True((await Read<SupplementDto>(await client.PutAsync($"/api/health/supplements/{supplement.Id}/image", form))).HasImage);

        var onDisk = await File.ReadAllBytesAsync(Assert.Single(Directory.GetFiles(storage)));
        Assert.False(onDisk.AsSpan().IndexOf(png.AsSpan(0, 8)) >= 0);
        Assert.Equal(png, await client.GetByteArrayAsync($"/api/health/supplements/{supplement.Id}/image"));
    }

    [Fact]
    public async Task Another_member_of_the_same_household_sees_none_of_it()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        var ownerId = Guid.NewGuid();
        using var owner = factory.CreateClient().AsUser(ownerId);
        var supplement = await Read<SupplementDto>(await owner.PostAsJsonAsync("/api/health/supplements", Magnesium()));

        // A second account joins the owner's household (members are not created through the API yet).
        var memberId = Guid.NewGuid();
        using var member = factory.CreateClient().AsUser(memberId);
        await member.GetAsync("/api/stores");
        await using (var db = factory.CreateDbContext())
        {
            var household = db.HouseholdMembers.Single(m => m.UserId == ownerId).HouseholdId;
            db.HouseholdMembers.RemoveRange(db.HouseholdMembers.Where(m => m.UserId == memberId));
            db.HouseholdMembers.Add(HouseholdMember.Rehydrate(Guid.NewGuid(), household, memberId, HouseholdRole.Member, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }
        using var stranger = factory.CreateClient().AsUser(Guid.NewGuid());
        foreach (var other in new[] { member, stranger })
        {
            Assert.Empty((await Read<SupplementListDto>(await other.GetAsync("/api/health/supplements?includeStopped=true"))).Supplements);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/health/supplements/{supplement.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/health/supplements/{supplement.Id}/intakes", new SupplementIntakeRequest(null))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/health/supplements/{supplement.Id}", Magnesium())).StatusCode);
        }
    }
}
