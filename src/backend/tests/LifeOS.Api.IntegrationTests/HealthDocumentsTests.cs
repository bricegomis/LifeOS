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

/// <summary>ADR 0018: health documents (encrypted files) and appointments, private to their account.</summary>
[Collection(PostgresCollection.Name)]
public sealed class HealthDocumentsTests(PostgresContainerFixture postgres)
{
    private static readonly byte[] Pdf = "%PDF-1.4\n1 0 obj\n<<>>\nendobj\ntrailer\n<<>>\n%%EOF"u8.ToArray();

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static MultipartFormDataContent PdfForm()
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Pdf);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "bilan.pdf");
        return form;
    }

    [Fact]
    public async Task A_document_keeps_its_files_encrypted_and_its_linked_analyses()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var storage = factory.Services.GetRequiredService<IOptions<HealthFileStorageOptions>>().Value.StoragePath;
        var before = Directory.Exists(storage) ? Directory.GetFiles(storage).Length : 0;

        var doc = await Read<HealthDocumentDto>(await client.PostAsJsonAsync("/api/health/documents",
            new HealthDocumentRequest("resultat", "Bilan sanguin annuel", null, "Cerballiance", "À jeun")));
        var withFile = await Read<HealthDocumentDto>(await client.PostAsync($"/api/health/documents/{doc.Id}/files", PdfForm()));
        var file = Assert.Single(withFile.Files);
        Assert.Equal(("application/pdf", "bilan.pdf"), (file.ContentType, file.OriginalName));
        Assert.Equal(before + 1, Directory.GetFiles(storage).Length);
        foreach (var path in Directory.GetFiles(storage))
            Assert.False((await File.ReadAllBytesAsync(path)).AsSpan().IndexOf("%PDF"u8) >= 0);
        Assert.Equal(Pdf, await client.GetByteArrayAsync($"/api/health/documents/{doc.Id}/files/{file.Id}"));

        var marker = await Read<MarkerDto>(await client.PostAsJsonAsync("/api/health/markers", new MarkerRequest("Ferritine", "µg/L", 30, 400, 80, 150, 6, null)));
        var analysis = await Read<AnalysisDto>(await client.PostAsJsonAsync("/api/health/analyses", new AnalysisRequest(null, "Cerballiance", null, doc.Id,
            [new AnalysisValueRequest(marker.Id, 90)])));
        Assert.Equal([analysis.Id], (await Read<HealthDocumentDto>(await client.GetAsync($"/api/health/documents/{doc.Id}"))).AnalysisIds);
        Assert.Single(await Read<List<HealthDocumentDto>>(await client.GetAsync("/api/health/documents?type=resultat&q=cerballiance")));
        Assert.Empty(await Read<List<HealthDocumentDto>>(await client.GetAsync("/api/health/documents?type=ordonnance")));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/health/documents", new HealthDocumentRequest("facture", "X", null, null, null))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/health/documents/{doc.Id}")).StatusCode);
        Assert.Equal(before, Directory.GetFiles(storage).Length);
        Assert.Null((await Read<AnalysisDto>(await client.GetAsync($"/api/health/analyses/{analysis.Id}"))).DocumentId);
    }

    [Fact]
    public async Task Appointments_split_upcoming_and_past_and_link_their_report()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var today = (await Read<AppointmentListDto>(await client.GetAsync("/api/health/appointments"))).Today;
        var report = await Read<HealthDocumentDto>(await client.PostAsJsonAsync("/api/health/documents", new HealthDocumentRequest("compte_rendu", "CR cardiologue", null, null, null)));
        var past = await Read<AppointmentDto>(await client.PostAsJsonAsync("/api/health/appointments", new AppointmentRequest(today.AddDays(-10), new TimeOnly(9, 30),
            "Cardiologue", "Contrôle", null, null, "https://www.doctolib.fr/cardiologue/paris/dr-x", report.Id)));
        Assert.Equal("CR cardiologue", past.ReportTitle);
        await Read<AppointmentDto>(await client.PostAsJsonAsync("/api/health/appointments", new AppointmentRequest(today.AddDays(5), null, "Dentiste", null, null, null, null, null)));
        var list = await Read<AppointmentListDto>(await client.GetAsync("/api/health/appointments"));
        Assert.Equal(["Dentiste"], list.Upcoming.Select(a => a.Who));
        Assert.Equal(["Cardiologue"], list.Past.Select(a => a.Who));

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/health/appointments", new AppointmentRequest(today, null, "X", null, null, null, "http://insecure.example", null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/health/appointments", new AppointmentRequest(today, null, "X", null, null, null, null, Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/health/documents/{report.Id}")).StatusCode);
        Assert.Null((await Read<AppointmentListDto>(await client.GetAsync("/api/health/appointments"))).Past.Single().ReportDocumentId);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/health/appointments/{past.Id}")).StatusCode);
    }

    [Fact]
    public async Task Another_member_of_the_same_household_sees_none_of_it()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        var ownerId = Guid.NewGuid();
        using var owner = factory.CreateClient().AsUser(ownerId);
        var doc = await Read<HealthDocumentDto>(await owner.PostAsJsonAsync("/api/health/documents", new HealthDocumentRequest("ordonnance", "Ordonnance", null, null, null)));
        var file = Assert.Single((await Read<HealthDocumentDto>(await owner.PostAsync($"/api/health/documents/{doc.Id}/files", PdfForm()))).Files);
        var appointment = await Read<AppointmentDto>(await owner.PostAsJsonAsync("/api/health/appointments", new AppointmentRequest(new DateOnly(2030, 1, 1), null, "Généraliste", null, null, null, null, null)));

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
            Assert.Empty(await Read<List<HealthDocumentDto>>(await other.GetAsync("/api/health/documents")));
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/health/documents/{doc.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/health/documents/{doc.Id}/files/{file.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/health/documents/{doc.Id}")).StatusCode);
            var list = await Read<AppointmentListDto>(await other.GetAsync("/api/health/appointments"));
            Assert.Empty(list.Upcoming.Concat(list.Past));
            Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/health/appointments/{appointment.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync("/api/health/appointments", new AppointmentRequest(new DateOnly(2030, 1, 1), null, "X", null, null, null, null, doc.Id))).StatusCode);
        }
    }
}
