using LifeOS.Api.Food.Files;
using LifeOS.Domain.Food.Foods;
using LifeOS.Infrastructure.Food.Files;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Food.Foods;

/// <summary>
/// Food management shared by the endpoints and the assistant tools (ADR 0011): plain DbContext
/// access on the Food tables, rules in <see cref="FoodItem"/>. Every method is scoped to one
/// household; a null result means "not in this household".
/// </summary>
public sealed class FoodLibrary(LifeOSDbContext db, FoodImageStorage images, IRemoteImageSource remoteImages,
    ILogger<FoodLibrary> logger)
{
    /// <param name="search">Optional: name contains this text, case and accents as typed.</param>
    public async Task<List<FoodItem>> ListAsync(Guid householdId, string? search = null, bool includeArchived = true,
        CancellationToken ct = default)
    {
        var query = db.FoodItems.AsNoTracking().Where(f => f.HouseholdId == householdId);
        if (!includeArchived) query = query.Where(f => !f.IsArchived);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(f => EF.Functions.ILike(f.Name, $"%{EscapeLike(search.Trim())}%", "\\"));
        return await query.OrderBy(f => f.Name).ToListAsync(ct);
    }

    public Task<FoodItem?> FindAsync(Guid householdId, Guid id, CancellationToken ct) =>
        db.FoodItems.SingleOrDefaultAsync(f => f.Id == id && f.HouseholdId == householdId, ct);

    public async Task<FoodItem> CreateAsync(Guid householdId, FoodRequest request, CancellationToken ct)
    {
        var food = FoodItem.CreateManual(householdId, request.Name, request.ReferenceUnit ?? "", request.Nutrition?.ToDomain());
        food.UpdateDigestiveInformation(request.Fodmap, request.Nova, request.DigestiveNotes);
        db.FoodItems.Add(food);
        await db.SaveChangesAsync(ct);
        return food;
    }

    public async Task<FoodItem?> UpdateAsync(Guid householdId, Guid id, FoodRequest request, CancellationToken ct)
    {
        var food = await FindAsync(householdId, id, ct);
        if (food is null) return null;
        food.UpdateDetails(request.Name, request.ReferenceUnit ?? "", request.Nutrition?.ToDomain());
        food.UpdateDigestiveInformation(request.Fodmap, request.Nova, request.DigestiveNotes);
        await db.SaveChangesAsync(ct);
        return food;
    }

    public async Task<FoodItem?> ArchiveAsync(Guid householdId, Guid id, CancellationToken ct)
    {
        var food = await FindAsync(householdId, id, ct);
        if (food is null) return null;
        food.Archive();
        await db.SaveChangesAsync(ct);
        return food;
    }

    public async Task<FoodItem?> ReplaceImageAsync(Guid householdId, Guid id, Stream content, long length, string? contentType,
        CancellationToken ct)
    {
        var food = await FindAsync(householdId, id, ct);
        if (food is null) return null;
        await images.ReplaceImageAsync(content, length, contentType, food.ImagePath,
            stored => food.SetImage(stored.FileName, stored.ContentType), () => db.SaveChangesAsync(ct), logger, ct);
        return food;
    }

    /// <summary>Downloads an image found by the assistant, then stores it like an upload.</summary>
    public async Task<FoodItem?> ReplaceImageFromUrlAsync(Guid householdId, Guid id, string url, CancellationToken ct)
    {
        if (await FindAsync(householdId, id, ct) is null) return null;
        var image = await remoteImages.DownloadAsync(url, ct);
        using var content = new MemoryStream(image.Bytes);
        return await ReplaceImageAsync(householdId, id, content, image.Bytes.Length, image.ContentType, ct);
    }

    public async Task<FoodItem?> RemoveImageAsync(Guid householdId, Guid id, CancellationToken ct)
    {
        var food = await FindAsync(householdId, id, ct);
        if (food is null) return null;
        var oldPath = food.ImagePath;
        food.RemoveImage();
        await db.SaveChangesAsync(ct);
        images.DeleteQuietly(oldPath, e => logger.LogError(e, "Unable to remove deleted food image"));
        return food;
    }

    internal static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
