using System.ComponentModel.DataAnnotations;

namespace LifeOS.Api.Authentication;

/// <summary>
/// Options bound from the "Supabase" configuration section, used to validate the JWTs
/// issued by Supabase Auth for the frontend's authenticated users.
/// </summary>
public sealed class SupabaseAuthOptions
{
    public const string SectionName = "Supabase";

    /// <summary>
    /// Base URL of the Supabase project, e.g. "https://xxxxxxxx.supabase.co".
    /// </summary>
    [Required]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Expected "aud" claim on access tokens issued by Supabase Auth.
    /// </summary>
    public string Audience { get; set; } = "authenticated";

    public string Issuer => $"{Url.TrimEnd('/')}/auth/v1";
}
