namespace LifeOS.Domain.Food;

/// <summary>Rules shared by food and recipe images: a stored file name and an accepted format.</summary>
public static class Images
{
    public static void Validate(string imagePath, string imageContentType)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || imagePath.Length > 500)
            throw new ArgumentException("Référence d'image invalide.");
        if (imageContentType is not ("image/jpeg" or "image/png" or "image/webp"))
            throw new ArgumentException("Format d'image invalide.");
    }
}
