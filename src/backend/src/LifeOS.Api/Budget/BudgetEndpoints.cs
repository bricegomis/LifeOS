using LifeOS.Api.Files;
using LifeOS.Api.Households;
using LifeOS.Api.Validation;
using LifeOS.Application.Budget;
using LifeOS.Domain.Budget;

namespace LifeOS.Api.Budget;

/// <summary>An operation classified, and the rule created at the same time if any.</summary>
public sealed record BudgetClassifyResultDto(BudgetOperationDto Operation, BudgetRuleDto? Rule, int Classified);

public static class BudgetModule
{
    /// <summary>Management code shared by the Budget endpoints and the assistant tools (ADR 0020).</summary>
    public static IServiceCollection AddBudgetLibraries(this IServiceCollection services) => services.AddScoped<BudgetLibrary>();

    public static IEndpointRouteBuilder MapBudgetEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/budget").RequireAuthorization().WithTags("Budget").AddRequestValidation();

        g.MapGet("/lines", async (bool? includeStopped, HouseholdContext household, BudgetLibrary budget, CancellationToken ct) =>
            Results.Ok(await budget.ListLinesAsync(household, includeStopped ?? false, ct))).Produces<BudgetLineListDto>();
        g.MapGet("/lines/{id:guid}", async (Guid id, HouseholdContext household, BudgetLibrary budget, CancellationToken ct) =>
            await budget.GetLineAsync(household, id, ct) is { } line ? Results.Ok(line) : Results.NotFound())
            .Produces<BudgetLineDetailDto>().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/lines", async (BudgetLineRequest request, HouseholdContext household, BudgetLibrary budget, CancellationToken ct) =>
        {
            var line = await budget.CreateLineAsync(household, request, ct);
            return Results.Created($"/api/budget/lines/{line.Id}", line);
        }).Produces<BudgetLineDto>(StatusCodes.Status201Created).ProducesValidationProblem();
        g.MapPut("/lines/{id:guid}", async (Guid id, BudgetLineRequest request, HouseholdContext household, BudgetLibrary budget, CancellationToken ct) =>
            Found(await budget.UpdateLineAsync(household, id, request, ct))).Produces<BudgetLineDto>().ProducesValidationProblem();
        g.MapPost("/lines/{id:guid}/stop", async (Guid id, BudgetStopRequest request, HouseholdContext household, BudgetLibrary budget, CancellationToken ct) =>
            Found(await budget.StopLineAsync(household, id, request.FromMonth, ct))).Produces<BudgetLineDto>();
        g.MapPost("/lines/{id:guid}/resume", async (Guid id, HouseholdContext household, BudgetLibrary budget, CancellationToken ct) =>
            Found(await budget.ResumeLineAsync(household, id, ct))).Produces<BudgetLineDto>();
        g.MapDelete("/lines/{id:guid}", async (Guid id, HouseholdContext household, BudgetLibrary budget, CancellationToken ct) =>
            await budget.DeleteLineAsync(household, id, ct) ? Results.NoContent() : Results.NotFound())
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status409Conflict);

        g.MapGet("/operations", async (string? month, string? filter, string? q, HouseholdContext household, BudgetLibrary budget, CancellationToken ct) =>
            Results.Ok(await budget.ListOperationsAsync(household, month, filter, q, ct))).Produces<BudgetOperationListDto>();
        g.MapPost("/operations", async (BudgetManualOperationRequest request, HouseholdContext household, BudgetLibrary budget, CancellationToken ct) =>
        {
            var operation = await budget.AddOperationAsync(household, request, "hand", ct);
            return Results.Created($"/api/budget/operations/{operation.Id}", operation);
        }).Produces<BudgetOperationDto>(StatusCodes.Status201Created).ProducesValidationProblem();
        g.MapPut("/operations/{id:guid}/classification", async (Guid id, BudgetClassifyRequest request, HouseholdContext household, BudgetLibrary budget,
            CancellationToken ct) => await budget.ClassifyAsync(household, id, request, "hand", ct) is { } r
                ? Results.Ok(new BudgetClassifyResultDto(r.Operation, r.Rule, r.Classified)) : Results.NotFound())
            .Produces<BudgetClassifyResultDto>().ProducesValidationProblem();
        g.MapDelete("/operations/{id:guid}", async (Guid id, HouseholdContext household, BudgetLibrary budget, CancellationToken ct) =>
            await budget.DeleteOperationAsync(household, id, ct) is null ? Results.NotFound() : Results.NoContent())
            .Produces(StatusCodes.Status204NoContent);
        g.MapPost("/imports", async (HttpRequest request, HouseholdContext household, BudgetLibrary budget, CancellationToken ct) =>
        {
            var file = await request.ReadFileAsync("file", "Choisissez un relevé OFX.", ct);
            if (file.Length > OfxStatement.MaxBytes) throw new ArgumentException("Relevé trop gros (5 Mo maximum).");
            using var memory = new MemoryStream();
            await file.CopyToAsync(memory, ct);
            return Results.Ok(await budget.ImportAsync(household, memory.ToArray(), ct));
        }).WithUploadLimit().Produces<BudgetImportDto>().ProducesValidationProblem();

        g.MapGet("/rules", async (HouseholdContext household, BudgetLibrary budget, CancellationToken ct) =>
            Results.Ok(await budget.ListRulesAsync(household, ct))).Produces<IReadOnlyList<BudgetRuleDto>>();
        g.MapPost("/rules", async (BudgetRuleRequest request, HouseholdContext household, BudgetLibrary budget, CancellationToken ct) =>
        {
            var result = await budget.CreateRuleAsync(household, request, ct);
            return Results.Created($"/api/budget/rules/{result.Rule.Id}", result);
        }).Produces<BudgetRuleResultDto>(StatusCodes.Status201Created).ProducesValidationProblem();
        g.MapPut("/rules/{id:guid}", async (Guid id, BudgetRuleRequest request, HouseholdContext household, BudgetLibrary budget, CancellationToken ct) =>
            await budget.UpdateRuleAsync(household, id, request, ct) is { } r ? Results.Ok(r) : Results.NotFound())
            .Produces<BudgetRuleResultDto>().ProducesValidationProblem();
        g.MapPost("/rules/{id:guid}/move", async (Guid id, BudgetRuleMoveRequest request, HouseholdContext household, BudgetLibrary budget, CancellationToken ct) =>
            await budget.MoveRuleAsync(household, id, request.Position, ct) is { } rules ? Results.Ok(rules) : Results.NotFound())
            .Produces<IReadOnlyList<BudgetRuleDto>>();
        g.MapDelete("/rules/{id:guid}", async (Guid id, HouseholdContext household, BudgetLibrary budget, CancellationToken ct) =>
            await budget.DeleteRuleAsync(household, id, ct) ? Results.NoContent() : Results.NotFound()).Produces(StatusCodes.Status204NoContent);

        g.MapGet("/months/{month}", async (string month, HouseholdContext household, BudgetLibrary budget, CancellationToken ct) =>
            Results.Ok(await budget.MonthAsync(household, month, ct))).Produces<BudgetMonthDto>();
        g.MapGet("/trend", async (int? months, string? until, HouseholdContext household, BudgetLibrary budget, CancellationToken ct) =>
            Results.Ok(await budget.TrendAsync(household, months ?? 6, until, ct))).Produces<BudgetTrendDto>();
        return app;
    }

    private static IResult Found<T>(T? value) where T : class => value is null ? Results.NotFound() : Results.Ok(value);
}
