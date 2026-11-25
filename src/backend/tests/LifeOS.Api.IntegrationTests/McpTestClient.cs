using ModelContextProtocol.Client;

namespace LifeOS.Api.IntegrationTests;

/// <summary>Connects an MCP client to the test API, as Claude does, for the signed-in test user of <paramref name="http"/>.</summary>
internal static class McpTestClient
{
    public static Task<McpClient> ConnectMcpAsync(this HttpClient http) =>
        McpClient.CreateAsync(
            new HttpClientTransport(
                new HttpClientTransportOptions { Endpoint = new Uri(http.BaseAddress!, "/mcp"), TransportMode = HttpTransportMode.StreamableHttp },
                http),
            // The client first probes with server/discover and gives up after 5 s by default. The first MCP request of a
            // test server builds every tool schema and can take longer on a busy machine; the probe was then cancelled,
            // answered 499 and failed the test at random. The usual 60 s initialization timeout covers it instead.
            new McpClientOptions { DiscoverProbeTimeout = Timeout.InfiniteTimeSpan });
}
