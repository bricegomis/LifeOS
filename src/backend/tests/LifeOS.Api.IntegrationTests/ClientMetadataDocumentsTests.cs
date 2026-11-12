using LifeOS.Api.Authentication;

namespace LifeOS.Api.IntegrationTests;

/// <summary>Pure rules of the client ID metadata documents (ADR 0011); no database.</summary>
public sealed class ClientMetadataDocumentsTests
{
    private static readonly string[] Trusted = ["claude.ai", "claude.com"];
    private static readonly Uri ClaudeCode = new("https://claude.ai/oauth/claude-code-client-metadata");

    [Theory]
    [InlineData("https://claude.ai/oauth/claude-code-client-metadata", true)]
    [InlineData("https://claude.com/oauth/mcp-client", true)]
    [InlineData("https://evil.example/oauth/client", false)]
    [InlineData("https://claude.ai.evil.example/client", false)]
    [InlineData("http://claude.ai/oauth/client", false)]
    [InlineData("https://user@claude.ai/oauth/client", false)]
    [InlineData("https://claude.ai/", false)]
    [InlineData("lifeos-web", false)]
    [InlineData(null, false)]
    public void Only_https_documents_on_trusted_hosts_are_considered(string? clientId, bool trusted) =>
        Assert.Equal(trusted, ClientMetadataDocuments.IsTrustedClientId(clientId, Trusted, out _));

    [Fact]
    public void A_document_must_name_itself_and_declare_safe_redirects()
    {
        var valid = ClientMetadataDocuments.Parse(ClaudeCode, """
            {"client_id":"https://claude.ai/oauth/claude-code-client-metadata","client_name":"Claude Code",
             "redirect_uris":["http://localhost/callback","http://127.0.0.1/callback"],"token_endpoint_auth_method":"none"}
            """);
        Assert.NotNull(valid);
        Assert.Equal("Claude Code", valid.ClientName);
        Assert.Equal(2, valid.RedirectUris.Count);

        Assert.Null(ClientMetadataDocuments.Parse(ClaudeCode, """{"client_id":"https://claude.ai/other","redirect_uris":["https://claude.ai/cb"]}"""));
        Assert.Null(ClientMetadataDocuments.Parse(ClaudeCode, """{"client_id":"https://claude.ai/oauth/claude-code-client-metadata","redirect_uris":["http://evil.example/cb"]}"""));
        Assert.Null(ClientMetadataDocuments.Parse(ClaudeCode, """{"client_id":"https://claude.ai/oauth/claude-code-client-metadata","redirect_uris":[]}"""));
        Assert.Null(ClientMetadataDocuments.Parse(ClaudeCode, """{"client_id":"https://claude.ai/oauth/claude-code-client-metadata","redirect_uris":["https://claude.ai/cb"],"token_endpoint_auth_method":"private_key_jwt"}"""));
        Assert.Null(ClientMetadataDocuments.Parse(ClaudeCode, "not json"));
    }

    [Fact]
    public void Loopback_redirects_match_whatever_the_port_and_others_match_exactly()
    {
        var metadata = new ClientMetadata(ClaudeCode, "Claude Code",
            [new Uri("http://localhost/callback"), new Uri("http://127.0.0.1/callback"), new Uri("https://claude.ai/api/mcp/auth_callback")]);

        Assert.True(ClientMetadataDocuments.AllowsRedirect(metadata, new Uri("http://localhost:51234/callback")));
        Assert.True(ClientMetadataDocuments.AllowsRedirect(metadata, new Uri("http://127.0.0.1:3118/callback")));
        Assert.True(ClientMetadataDocuments.AllowsRedirect(metadata, new Uri("https://claude.ai/api/mcp/auth_callback")));
        Assert.False(ClientMetadataDocuments.AllowsRedirect(metadata, new Uri("http://localhost:51234/other")));
        Assert.False(ClientMetadataDocuments.AllowsRedirect(metadata, new Uri("http://localhost:51234/callback?x=1")));
        Assert.False(ClientMetadataDocuments.AllowsRedirect(metadata, new Uri("https://claude.ai:8443/api/mcp/auth_callback")));
        Assert.False(ClientMetadataDocuments.AllowsRedirect(metadata, new Uri("https://evil.example/api/mcp/auth_callback")));
    }
}
