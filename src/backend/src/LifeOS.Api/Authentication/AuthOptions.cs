namespace LifeOS.Api.Authentication;

/// <summary>
/// "Auth" configuration section (ADR 0011): LifeOS signs users in with Google and issues its
/// own OAuth tokens.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Public base URL of the API, e.g. "https://api.lifeos.example". Derived from the request when empty.</summary>
    public string? Issuer { get; set; }

    /// <summary>Only these verified Google addresses can sign in.</summary>
    public string[] AllowedEmails { get; set; } = [];

    public GoogleOptions Google { get; set; } = new();

    /// <summary>RSA private key (PEM) signing the tokens. Required outside Development and Testing.</summary>
    public string? SigningKey { get; set; }

    /// <summary>256-bit key (base64) encrypting the tokens. Required outside Development and Testing.</summary>
    public string? EncryptionKey { get; set; }

    public WebClientOptions WebClient { get; set; } = new();

    public sealed class GoogleOptions
    {
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
    }

    /// <summary>The LifeOS Angular app (web and iPhone): public client, PKCE, no consent screen.</summary>
    public sealed class WebClientOptions
    {
        public const string ClientId = "lifeos-web";

        /// <summary>Where the app receives the code: its base URL(s) and <c>lifeos://auth/callback</c>.</summary>
        public string[] RedirectUris { get; set; } = [];
    }
}
