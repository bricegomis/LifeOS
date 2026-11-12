using LifeOS.Api.Authentication;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;

namespace LifeOS.Api.Assistant;

/// <summary>
/// The MCP endpoint (ADR 0011): Claude reads and fills LifeOS through tools that call the same
/// use cases as the app. The API calls no AI provider and holds no prompt; tool descriptions
/// are documentation. Each module declares its tools in its own folder
/// (<c>[McpServerToolType]</c> classes), discovered from this assembly.
/// </summary>
public static class AssistantSetup
{
    public const string Path = "/mcp";

    public static void AddLifeOSAssistant(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();
        builder.Services
            .AddMcpServer(server => server.ServerInfo = new() { Name = "LifeOS", Version = "1.0" })
            // Stateless: every request carries its token and is answered on its own.
            .WithHttpTransport(http => http.Stateless = true)
            .WithToolsFromAssembly(typeof(AssistantSetup).Assembly)
            .WithRequestFilters(filters => filters.AddCallToolFilter(next => async (context, ct) =>
            {
                // Same mapping as ApiExceptionHandler: the assistant reads the rule that was broken.
                try { return await next(context, ct); }
                catch (ArgumentException exception) { throw new McpException(exception.Message); }
                catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException { SqlState: "23505" })
                { throw new McpException("Cet élément existe déjà."); }
                catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException { SqlState: "23503" })
                { throw new McpException("Cet élément est encore utilisé ailleurs et ne peut pas être supprimé."); }
            }));
    }

    public static void MapLifeOSAssistant(this WebApplication app)
    {
        app.MapProtectedResourceMetadata();
        app.MapMcp(Path).RequireAuthorization(AssistantAuthentication.Policy);
    }
}
