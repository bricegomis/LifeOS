using System.ComponentModel.DataAnnotations;
using LifeOS.Api.Households;
using ModelContextProtocol;

namespace LifeOS.Api.Assistant;

/// <summary>
/// The caller of an MCP tool: the signed-in account and its household, resolved exactly as for
/// an endpoint (<see cref="HouseholdContext"/>). Every tool filters on its household.
/// </summary>
public static class AssistantRequest
{
    public static async Task<HouseholdContext> HouseholdAsync(IHttpContextAccessor accessor) =>
        await HouseholdContext.BindAsync(accessor.HttpContext
            ?? throw new InvalidOperationException("MCP tools run inside an HTTP request."))
        ?? throw new UnauthorizedAccessException("Foyer introuvable.");

    /// <summary>Applies the same data annotations as the endpoints' request validation.</summary>
    public static T Validated<T>(T request) where T : notnull
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true))
            throw new ArgumentException(string.Join(" ", errors.Select(error =>
                $"{string.Join(", ", error.MemberNames)} : {error.ErrorMessage}")));
        return request;
    }

    /// <summary>The error the assistant gets for an id that is not in the caller's household.</summary>
    public static McpException NotFound(string what) => new($"{what} introuvable dans votre foyer.");

    /// <summary>Unwraps a library result: null means "not in the household".</summary>
    public static T OrNotFound<T>(this T? value, string what) where T : class => value ?? throw NotFound(what);
}
