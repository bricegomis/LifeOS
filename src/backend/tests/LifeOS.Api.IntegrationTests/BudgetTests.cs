using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LifeOS.Api.Budget;
using LifeOS.Api.Health;
using LifeOS.Application.Budget;
using LifeOS.Domain.Households;
using Microsoft.AspNetCore.Mvc.Testing;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LifeOS.Api.IntegrationTests;

/// <summary>ADR 0020: the budget, its OFX imports, rules and months, private to its account and usable from the assistant.</summary>
[Collection(PostgresCollection.Name)]
public sealed class BudgetTests(PostgresContainerFixture postgres)
{
    private const string Statement = """
        <?xml version="1.0" encoding="utf-8"?>
        <OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS><CURDEF>EUR</CURDEF><BANKACCTFROM><BANKID>x</BANKID><ACCTID>00099</ACCTID><ACCTTYPE>CHECKINGS</ACCTTYPE></BANKACCTFROM><AVAILBAL><DTASOF>20261030000000</DTASOF><BALAMT>1245.3</BALAMT></AVAILBAL><BANKTRANLIST><STMTTRN><DTPOSTED>20261030</DTPOSTED><TRNTYPE>DEBIT</TRNTYPE><TRNAMT>-37.00</TRNAMT><FITID>t1</FITID><NAME>CARTE 29/10/26 LE PASSIONNE CB*1234</NAME></STMTTRN><STMTTRN><DTPOSTED>20261030</DTPOSTED><TRNTYPE>CREDIT</TRNTYPE><TRNAMT>2929.36</TRNAMT><FITID>t2</FITID><NAME>VIR SEPA EMPLOYEUR</NAME></STMTTRN><STMTTRN><DTPOSTED>20261028</DTPOSTED><TRNTYPE>DEBIT</TRNTYPE><TRNAMT>-45.58</TRNAMT><FITID>t3</FITID><NAME>CARTE 25/10/26 CARREFOUR CONTA 4 CB*1234</NAME></STMTTRN><STMTTRN><DTPOSTED>20261023</DTPOSTED><TRNTYPE>DEBIT</TRNTYPE><TRNAMT>-23.00</TRNAMT><FITID>t4</FITID><NAME>CARTE 18/10/26 LE PASSIONNE CB*1234</NAME></STMTTRN><STMTTRN><DTPOSTED>20261021</DTPOSTED><TRNTYPE>DEBIT</TRNTYPE><TRNAMT>-112.00</TRNAMT><FITID>t5</FITID><NAME>PRLV SEPA PayPal Europe S.a.r.l. et Cie</NAME></STMTTRN><STMTTRN><DTPOSTED>20261012</DTPOSTED><TRNTYPE>DEBIT</TRNTYPE><TRNAMT>-500.00</TRNAMT><FITID>t6</FITID><NAME>VIR SEPA MOI EPARGNE</NAME></STMTTRN></BANKTRANLIST></STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>
        """;

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static MultipartFormDataContent Ofx(string text)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/x-ofx");
        form.Add(file, "file", "export.ofx");
        return form;
    }

    private static Task<HttpResponseMessage> AddLine(HttpClient client, string name, string kind, string? category, decimal? amount = null,
        bool supplements = false, string? period = null) =>
        client.PostAsJsonAsync("/api/budget/lines", new BudgetLineRequest(name, category, kind, false, supplements, null, amount, period));

    [Fact]
    public async Task A_statement_is_imported_once_classified_by_rules_and_read_as_a_month()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var food = await Read<BudgetLineDto>(await AddLine(client, "Courses", "envelope", "Alimentation", 800));
        var outings = await Read<BudgetLineDto>(await AddLine(client, "Sorties", "envelope", "Sorties", 100));
        var salary = await Read<BudgetLineDto>(await AddLine(client, "Salaire", "income", null, 2930));
        var savings = await Read<BudgetLineDto>(await AddLine(client, "Épargne", "off", null));
        var insurance = await Read<BudgetLineDto>(await AddLine(client, "Assurance voiture", "fixed", "Assurances", 300, period: "year"));
        Assert.Equal((25m, "Revenus"), (insurance.Planned, salary.Category));
        Assert.Equal(HttpStatusCode.BadRequest, (await AddLine(client, "Sans montant", "fixed", "Logement")).StatusCode);

        // Supplements come from Health through its published contract.
        await client.PostAsJsonAsync("/api/health/supplements", new SupplementRequest("Magnésium", null, 2, "gelule", "soir", 180, 29.9m, null, null,
            new SupplementPeriodRequest("year", null, null, null, null, null)));
        var supplements = await Read<BudgetLineDto>(await AddLine(client, "Compléments", "envelope", "Santé", supplements: true));
        Assert.Equal(9.97m, supplements.Planned);

        await Read<BudgetRuleResultDto>(await client.PostAsJsonAsync("/api/budget/rules", new BudgetRuleRequest("carrefour", food.Id, "out")));
        await Read<BudgetRuleResultDto>(await client.PostAsJsonAsync("/api/budget/rules", new BudgetRuleRequest("EMPLOYEUR", salary.Id, "in")));

        var import = await Read<BudgetImportDto>(await client.PostAsync("/api/budget/imports", Ofx(Statement)));
        Assert.Equal(("00099", 6, 0, 6, 2, 4, 1245.3m), (import.Account, import.Read, import.Duplicates, import.Created, import.Classified, import.Unclassified, import.Balance!.Value));
        var again = await Read<BudgetImportDto>(await client.PostAsync("/api/budget/imports", Ofx(Statement)));
        Assert.Equal((6, 0), (again.Duplicates, again.Created));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/budget/imports", Ofx("Date;Montant\n2026-10-01;-3"))).StatusCode);

        var list = await Read<BudgetOperationListDto>(await client.GetAsync("/api/budget/operations?month=2026-10&filter=unclassified"));
        Assert.Equal((4, "00099"), (list.UnclassifiedCount, list.Accounts.Single().Account));
        var passionne = list.Operations.First(o => o.Label.Contains("PASSIONNE") && o.Date == new DateOnly(2026, 10, 29));

        // Classifying by hand with a rule classifies the other matching operation right away.
        var classified = await Read<BudgetClassifyResultDto>(await client.PutAsJsonAsync($"/api/budget/operations/{passionne.Id}/classification",
            new BudgetClassifyRequest(outings.Id, 1, "LE PASSIONNE")));
        Assert.Equal(("hand", 1, "out"), (classified.Operation.ClassifiedBy, classified.Classified, classified.Rule!.Direction));
        var transfer = list.Operations.Single(o => o.Label.Contains("EPARGNE"));
        await Read<BudgetClassifyResultDto>(await client.PutAsJsonAsync($"/api/budget/operations/{transfer.Id}/classification", new BudgetClassifyRequest(savings.Id)));
        var expenseInIncome = await client.PutAsJsonAsync($"/api/budget/operations/{transfer.Id}/classification", new BudgetClassifyRequest(salary.Id));
        Assert.Equal(HttpStatusCode.BadRequest, expenseInIncome.StatusCode);

        // A purchase typed by hand and spread over six months.
        await Read<BudgetOperationDto>(await client.PostAsJsonAsync("/api/budget/operations",
            new BudgetManualOperationRequest(new DateOnly(2026, 9, 12), "Compléments pour 6 mois", -412.2m, supplements.Id, 6)));

        var month = await Read<BudgetMonthDto>(await client.GetAsync("/api/budget/months/2026-10"));
        Assert.Equal((2929.36m, 2930m, 1, -112m, -500m), (month.Received, month.PlannedIncome, month.UnclassifiedCount, month.UnclassifiedTotal, month.Off));
        Assert.Equal(45.58m + 60 + 68.7m + 112, month.Spent);
        Assert.Equal(800 + 100 + 25 + 9.97m, month.PlannedExpenses);
        Assert.Equal(68.7m, month.Envelopes.Single(e => e.Line.Name == "Compléments").Real);

        var trend = await Read<BudgetTrendDto>(await client.GetAsync("/api/budget/trend?months=3&until=2026-11"));
        Assert.Equal(["2026-09", "2026-10", "2026-11"], trend.Months.Select(m => m.Month));
        Assert.Equal(68.7m, trend.Months[2].ByCategory["Santé"]);

        // Lines used by operations or rules are stopped, not deleted.
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/budget/lines/{food.Id}")).StatusCode);
        var stopped = await Read<BudgetLineDto>(await client.PostAsJsonAsync($"/api/budget/lines/{outings.Id}/stop", new BudgetStopRequest("2026-11")));
        Assert.Equal("2026-11", stopped.StoppedFrom);
        var detail = await Read<BudgetLineDetailDto>(await client.GetAsync($"/api/budget/lines/{food.Id}"));
        Assert.Equal(12, detail.Months.Count);
        Assert.Single(detail.Rules);

        var rules = await Read<List<BudgetRuleDto>>(await client.GetAsync("/api/budget/rules"));
        Assert.Equal(["carrefour", "EMPLOYEUR", "LE PASSIONNE"], rules.Select(r => r.Text));
        rules = await Read<List<BudgetRuleDto>>(await client.PostAsJsonAsync($"/api/budget/rules/{rules[2].Id}/move", new BudgetRuleMoveRequest(0)));
        Assert.Equal(["LE PASSIONNE", "carrefour", "EMPLOYEUR"], rules.Select(r => r.Text));
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/budget/rules/{rules[0].Id}")).StatusCode);
        Assert.Equal([0, 1], (await Read<List<BudgetRuleDto>>(await client.GetAsync("/api/budget/rules"))).Select(r => r.Position));
    }

    [Fact]
    public async Task Another_member_of_the_same_household_sees_none_of_it()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        var ownerId = Guid.NewGuid();
        using var owner = factory.CreateClient().AsUser(ownerId);
        var line = await Read<BudgetLineDto>(await AddLine(owner, "Courses", "envelope", "Alimentation", 800));
        var import = await Read<BudgetImportDto>(await owner.PostAsync("/api/budget/imports", Ofx(Statement)));
        var operation = import.Operations[0];
        var rule = (await Read<BudgetRuleResultDto>(await owner.PostAsJsonAsync("/api/budget/rules", new BudgetRuleRequest("X", line.Id)))).Rule;

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
            Assert.Empty((await Read<BudgetLineListDto>(await other.GetAsync("/api/budget/lines?includeStopped=true"))).Lines);
            Assert.Empty((await Read<BudgetOperationListDto>(await other.GetAsync("/api/budget/operations?month=2026-10"))).Operations);
            Assert.Empty(await Read<List<BudgetRuleDto>>(await other.GetAsync("/api/budget/rules")));
            Assert.Equal(0, (await Read<BudgetMonthDto>(await other.GetAsync("/api/budget/months/2026-10"))).Received);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/budget/lines/{line.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/budget/lines/{line.Id}", new BudgetLineRequest("X", "Y", "extra"))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/budget/operations/{operation.Id}/classification", new BudgetClassifyRequest(null))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/budget/rules/{rule.Id}")).StatusCode);
            // Their own line cannot point to the owner's operations or rules, nor theirs to the owner's line.
            var theirs = await Read<BudgetLineDto>(await AddLine(other, "Mes courses", "envelope", "Alimentation", 100));
            Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync("/api/budget/rules", new BudgetRuleRequest("X", line.Id))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync("/api/budget/operations",
                new BudgetManualOperationRequest(new DateOnly(2026, 10, 1), "X", -3, line.Id))).StatusCode);
            // The same statement imported by another account is theirs, not a duplicate of the owner's.
            Assert.Equal(6, (await Read<BudgetImportDto>(await other.PostAsync("/api/budget/imports", Ofx(Statement)))).Created);
            Assert.NotEqual(Guid.Empty, theirs.Id);
        }
    }

    [Fact]
    public async Task The_assistant_creates_lines_and_rules_and_classifies_what_rules_cannot()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        var user = Guid.NewGuid();
        using var http = factory.CreateClient().AsUser(user);
        await Read<BudgetImportDto>(await http.PostAsync("/api/budget/imports", Ofx(Statement)));
        await using var mcp = await Connect(factory, user);
        await using var stranger = await Connect(factory, Guid.NewGuid());

        var gifts = await Call(mcp, "create_budget_line", new() { ["name"] = "Cadeaux", ["kind"] = "extra", ["category"] = "Extras" });
        var giftsId = gifts.GetProperty("id").GetString();
        var food = await Call(mcp, "create_budget_line", new() { ["name"] = "Courses", ["kind"] = "envelope", ["category"] = "Alimentation", ["amount"] = 800, ["reduce"] = true });
        var updated = await Call(mcp, "update_budget_line", new() { ["lineId"] = food.GetProperty("id").GetString(), ["amount"] = 600, ["fromMonth"] = "2026-11" });
        Assert.Equal(2, updated.GetProperty("history").GetArrayLength());

        var rule = await Call(mcp, "create_budget_rule", new() { ["text"] = "CARREFOUR", ["lineId"] = food.GetProperty("id").GetString(), ["direction"] = "out" });
        Assert.Equal(1, rule.GetProperty("classified").GetInt32());

        var pending = await Call(mcp, "list_budget_operations", new() { ["month"] = "2026-10", ["filter"] = "unclassified", ["query"] = "paypal" });
        var paypal = pending.GetProperty("operations")[0].GetProperty("id").GetString();
        var result = await Call(mcp, "classify_budget_operation", new() { ["operationId"] = paypal, ["lineId"] = giftsId });
        Assert.Equal("assistant", result.GetProperty("operation").GetProperty("classifiedBy").GetString());

        var month = await Call(mcp, "get_budget_month", new() { ["month"] = "2026-10" });
        Assert.Equal(112, month.GetProperty("extras").GetDecimal());
        Assert.Contains("introuvable", await Error(stranger, "classify_budget_operation", new() { ["operationId"] = paypal, ["lineId"] = giftsId }));
        Assert.Empty((await Call(stranger, "list_budget_lines", new())).GetProperty("lines").EnumerateArray());
    }

    private static async Task<McpClient> Connect(WebApplicationFactory<Program> factory, Guid user)
    {
        var http = factory.CreateClient().AsUser(user);
        return await McpClient.CreateAsync(new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri(http.BaseAddress!, "/mcp"), TransportMode = HttpTransportMode.StreamableHttp },
            http));
    }

    private static async Task<JsonElement> Call(McpClient mcp, string tool, Dictionary<string, object?> arguments)
    {
        var result = await mcp.CallToolAsync(tool, Json(arguments));
        var text = Text(result);
        Assert.True(result.IsError != true, $"{tool} failed: {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static async Task<string> Error(McpClient mcp, string tool, Dictionary<string, object?> arguments)
    {
        var result = await mcp.CallToolAsync(tool, Json(arguments));
        Assert.True(result.IsError, $"{tool} should have failed: {Text(result)}");
        return Text(result);
    }

    private static Dictionary<string, object?> Json(Dictionary<string, object?> arguments) =>
        arguments.ToDictionary(pair => pair.Key, pair => (object?)JsonSerializer.SerializeToElement(pair.Value));

    private static string Text(CallToolResult result) =>
        string.Join("", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
}
