using LifeOS.Api.Assistant;
using LifeOS.Api.Authentication;
using LifeOS.Api.ErrorHandling;
using LifeOS.Api.Food.Foods;
using LifeOS.Api.Food.Products;
using LifeOS.Api.Food.Receipts;
using LifeOS.Api.Food.Recipes;
using LifeOS.Api.Food.Reviews;
using LifeOS.Api.Food.Stores;
using LifeOS.Api.Households;
using LifeOS.Api.Sports;
using LifeOS.Infrastructure;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddInfrastructure(builder.Configuration);

builder.AddLifeOSAuthentication();
builder.AddLifeOSAssistant();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>()?
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .ToArray() ?? [];

        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Applies pending EF Core migrations at startup so the schema is always up to date with the
// deployed code (ADR 0001). Skipped when explicitly disabled, e.g. by integration tests that
// manage migrations themselves against an ephemeral test database.
if (!builder.Configuration.GetValue<bool>("SkipDatabaseMigration"))
{
    using var migrationScope = app.Services.CreateScope();
    var dbContext = migrationScope.ServiceProvider.GetRequiredService<LifeOSDbContext>();
    dbContext.Database.Migrate();
    await OAuthClients.SynchronizeAsync(migrationScope.ServiceProvider);
}

var enableOpenApiUi = builder.Configuration.GetValue<bool?>("Api:EnableOpenApiUi")
    ?? app.Environment.IsDevelopment();

if (enableOpenApiUi)
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("LifeOS API"));
}

app.MapGet("/api/version", (HttpContext context, IConfiguration configuration) =>
{
    string? ReadBuildValue(string key) =>
        configuration[$"LifeOS:{key}"]?.Trim() is { Length: > 0 } value ? value : null;

    context.Response.Headers.CacheControl = "no-store";
    return Results.Ok(new
    {
        component = "api",
        buildId = ReadBuildValue("BuildId") ?? "local",
        buildNumber = ReadBuildValue("BuildNumber"),
        runId = ReadBuildValue("RunId"),
        runAttempt = ReadBuildValue("RunAttempt"),
        repository = ReadBuildValue("Repository"),
        serverUrl = ReadBuildValue("ServerUrl"),
        workflow = ReadBuildValue("Workflow"),
    });
}).AllowAnonymous();

app.UseLifeOSForwardedHeaders();
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors();
app.UseClientMetadataRegistration();
app.UseAuthentication();
app.UseAuthorization();

// Sign-in and OAuth, connected assistants and the MCP endpoint (ADR 0011)
app.MapAuthEndpoints();
app.MapConnectionsEndpoints();
app.MapLifeOSAssistant();
// Household core
app.MapHouseholdEndpoints();
app.MapProfilesEndpoints();
// Food module
app.MapFoodsEndpoints();
app.MapRecipesEndpoints();
app.MapRecipeCostEndpoints();
app.MapReviewsEndpoints();
app.MapStoresEndpoints();
app.MapProductsEndpoints();
app.MapReceiptsEndpoints();
// Sports module
app.MapSportTemplatesEndpoints();

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program;
