using LifeOS.Api.Authentication;
using LifeOS.Api.Endpoints;
using LifeOS.Infrastructure;
using LifeOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure();

builder.Services
    .AddOptions<SupabaseAuthOptions>()
    .Bind(builder.Configuration.GetSection(SupabaseAuthOptions.SectionName))
    .ValidateDataAnnotations();

if (builder.Environment.IsEnvironment("Testing"))
{
    // Integration tests authenticate via LifeOS.Api.Authentication.TestAuthHandler instead of
    // real Supabase JWTs, so multiple simulated users/households can be exercised deterministically.
    builder.Services
        .AddAuthentication(TestAuthHandler.SchemeName)
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>(
            TestAuthHandler.SchemeName, _ => { });
}
else
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer();

    builder.Services
        .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
        .Configure<IOptions<SupabaseAuthOptions>>((jwtOptions, supabaseOptions) =>
        {
            var supabase = supabaseOptions.Value;

            // Supabase Auth issues RS256-signed JWTs and exposes an OIDC discovery document,
            // so the signing keys are resolved automatically from its metadata endpoint.
            jwtOptions.Authority = supabase.Issuer;
            jwtOptions.Audience = supabase.Audience;
            jwtOptions.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            jwtOptions.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = supabase.Issuer,
                ValidateAudience = true,
                ValidAudience = supabase.Audience,
                ValidateLifetime = true,
            };
        });
}

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

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
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("LifeOS API"));
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapStoresEndpoints();
app.MapArticlesEndpoints();
app.MapRecipesEndpoints();
app.MapComposedMealsEndpoints();
app.MapWeekPlanningEndpoints();
app.MapLibraryEndpoints();
app.MapPlanningEndpoints();
app.MapWeekContextEndpoints();
app.MapFoodItemsEndpoints();
app.MapNutritionEndpoints();

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program;
