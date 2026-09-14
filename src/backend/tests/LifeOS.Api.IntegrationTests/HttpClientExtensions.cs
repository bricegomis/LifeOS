using LifeOS.Api.Authentication;

namespace LifeOS.Api.IntegrationTests;

internal static class HttpClientExtensions
{
    /// <summary>
    /// Attaches the <see cref="TestAuthHandler"/> header so the request is authenticated as the
    /// given simulated Supabase user (see <see cref="TestAuthHandler"/>).
    /// </summary>
    public static HttpClient AsUser(this HttpClient client, Guid supabaseUserId)
    {
        client.DefaultRequestHeaders.Remove(TestAuthHandler.SubHeaderName);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeaderName, supabaseUserId.ToString());
        return client;
    }
}
