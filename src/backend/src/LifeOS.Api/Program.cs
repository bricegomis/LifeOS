using LifeOS.Api.Authentication;
using LifeOS.Api.Endpoints;
using LifeOS.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
app.MapLibraryEndpoints();
app.MapPlanningEndpoints();
app.MapWeekContextEndpoints();

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program;
