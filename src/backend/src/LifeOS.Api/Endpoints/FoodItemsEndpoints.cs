using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Api.Contracts;
using LifeOS.Api.FoodItems;
using LifeOS.Api.Validation;
using LifeOS.Application.FoodItems;
using LifeOS.Application.Households;
using LifeOS.Domain.FoodItems;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace LifeOS.Api.Endpoints;

public static class FoodItemsEndpoints
{
    public static IEndpointRouteBuilder MapFoodItemsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/foods").RequireAuthorization().WithTags("Aliments").AddRequestValidation();
        g.MapGet("/", async (ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            var foods = await db.FoodItems.Where(f => f.HouseholdId == household).OrderBy(f => f.Name).ToListAsync(ct);
            return Results.Ok(foods.Select(FoodItemMapper.ToDto));
        });
        g.MapPost("/", async (FoodRequest request, ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var food = FoodItem.CreateManual(await households.ExecuteAsync(sub, ct), request.Name, request.ReferenceUnit ?? "",
                FoodItemMapper.NutritionFromDto(request.Nutrition));
            food.UpdateDigestiveInformation(request.Fodmap, request.Nova, request.DigestiveNotes);
            db.FoodItems.Add(food);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/foods/{food.Id}", FoodItemMapper.ToDto(food));
        });
        g.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            var food = await db.FoodItems.SingleOrDefaultAsync(f => f.Id == id && f.HouseholdId == household, ct);
            return food is null ? Results.NotFound() : Results.Ok(FoodItemMapper.ToDto(food));
        });
        g.MapPut("/{id:guid}", async (Guid id, FoodRequest request, ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            var food = await db.FoodItems.SingleOrDefaultAsync(f => f.Id == id && f.HouseholdId == household, ct);
            if (food is null) return Results.NotFound();
            food.UpdateDetails(request.Name, request.ReferenceUnit ?? "", FoodItemMapper.NutritionFromDto(request.Nutrition));
            food.UpdateDigestiveInformation(request.Fodmap, request.Nova, request.DigestiveNotes);
            await db.SaveChangesAsync(ct);
            return Results.Ok(FoodItemMapper.ToDto(food));
        });
        g.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            var food = await db.FoodItems.SingleOrDefaultAsync(f => f.Id == id && f.HouseholdId == household, ct);
            if (food is null) return Results.NotFound();
            food.Archive();
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
        g.MapGet("/{id:guid}/image", async (Guid id, ClaimsPrincipal user, HttpContext context,
            ResolveHouseholdForUserQuery households, LifeOSDbContext db, FoodImageStorage storage, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            var image = await db.FoodItems.Where(f => f.Id == id && f.HouseholdId == household)
                .Select(f => new { f.ImagePath, f.ImageContentType }).SingleOrDefaultAsync(ct);
            if (image?.ImagePath is null || image.ImageContentType is null) return Results.NotFound();
            if (!File.Exists(storage.ResolvePath(image.ImagePath))) return Results.NotFound();
            context.Response.Headers.CacheControl = "private, no-store";
            return Results.File(storage.OpenRead(image.ImagePath), image.ImageContentType);
        });
        g.MapPut("/{id:guid}/image", UploadImage)
            .WithMetadata(new RequestSizeLimitAttribute(11 * 1024 * 1024),
                new RequestFormLimitsAttribute { MultipartBodyLengthLimit = 11 * 1024 * 1024 });
        g.MapDelete("/{id:guid}/image", async (Guid id, ClaimsPrincipal user, ResolveHouseholdForUserQuery households,
            LifeOSDbContext db, FoodImageStorage storage, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            var food = await db.FoodItems.SingleOrDefaultAsync(f => f.Id == id && f.HouseholdId == household, ct);
            if (food is null) return Results.NotFound();
            var oldPath = food.ImagePath;
            food.RemoveImage();
            await db.SaveChangesAsync(ct);
            DeleteOldImage(storage, oldPath, loggerFactory);
            return Results.NoContent();
        });
        return app;
    }

    private static async Task<IResult> UploadImage(Guid id, HttpRequest request, ClaimsPrincipal user,
        ResolveHouseholdForUserQuery households, LifeOSDbContext db, FoodImageStorage storage,
        ILoggerFactory loggerFactory, CancellationToken ct)
    {
        if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
        var household = await households.ExecuteAsync(sub, ct);
        var food = await db.FoodItems.SingleOrDefaultAsync(f => f.Id == id && f.HouseholdId == household, ct);
        if (food is null) return Results.NotFound();
        var form = await request.ReadFormAsync(ct);
        var image = form.Files.GetFile("image");
        if (image is null) throw new ArgumentException("Sélectionnez une image d'aliment.");

        var oldPath = food.ImagePath;
        var stored = await storage.SaveAsync(image, ct);
        try
        {
            food.SetImage(stored.FileName, stored.ContentType);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            storage.Delete(stored.FileName);
            throw;
        }

        DeleteOldImage(storage, oldPath, loggerFactory);
        return Results.Ok(FoodItemMapper.ToDto(food));
    }

    private static void DeleteOldImage(FoodImageStorage storage, string? oldPath, ILoggerFactory loggerFactory)
    {
        if (oldPath is null) return;
        try { storage.Delete(oldPath); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            loggerFactory.CreateLogger("FoodImageStorage").LogError(exception, "Unable to remove replaced food image");
        }
    }
}
