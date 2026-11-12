using System.Net;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;

namespace LifeOS.Api.Authentication;

/// <summary>
/// « Autoriser … à accéder à LifeOS » (ADR 0011): shown once per user and Claude client. The
/// form posts the authorization request back with the decision and an antiforgery token.
/// </summary>
public static class ConsentPage
{
    public const string DecisionField = "lifeos_consent";
    public const string Accept = "accept";
    public const string Deny = "deny";

    private static readonly HashSet<string> NotReposted = [DecisionField, "__RequestVerificationToken", AuthenticationSetup.DeniedFlag];

    public static IResult Render(HttpContext context, IAntiforgery antiforgery, string clientName, string email, Uri redirectUri)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        var parameters = context.Request.HasFormContentType
            ? context.Request.Form.Select(pair => (pair.Key, Value: pair.Value.ToString()))
            : context.Request.Query.Select(pair => (pair.Key, Value: pair.Value.ToString()));

        var hidden = new StringBuilder();
        foreach (var (key, value) in parameters.Where(pair => !NotReposted.Contains(pair.Key)))
            hidden.Append($"<input type=\"hidden\" name=\"{E(key)}\" value=\"{E(value)}\">");
        hidden.Append($"<input type=\"hidden\" name=\"{E(tokens.FormFieldName)}\" value=\"{E(tokens.RequestToken ?? string.Empty)}\">");

        var loopback = ClientMetadataDocuments.IsLoopback(redirectUri);
        var destination = loopback
            ? "<p class=\"warning\"><strong>Retour vers un programme de cet ordinateur</strong> (" + E(redirectUri.Host) + "). "
              + "N’autorisez que si vous venez de lancer Claude Code vous-même.</p>"
            : "<p>Retour vers <strong>" + E(redirectUri.Host) + "</strong>.</p>";

        var html = $$"""
            <!doctype html>
            <html lang="fr">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Autoriser l’accès à LifeOS</title>
            <style>
              :root { color-scheme: light; font-family: Inter, ui-sans-serif, system-ui, -apple-system, "Segoe UI", sans-serif; }
              body { margin: 0; min-height: 100vh; display: grid; place-items: center; padding: 16px; background: #f7f3ea; color: #24251f; }
              main { width: min(100%, 32rem); display: grid; gap: 1rem; padding: clamp(1.25rem, 4vw, 2rem); border: 1px solid #e4dacb; border-radius: 24px; background: #fffdf8; box-shadow: 0 12px 32px rgba(36, 37, 31, .08); }
              h1 { margin: 0; font-size: 1.6rem; line-height: 1.2; }
              p, li { line-height: 1.5; margin: 0; }
              ul { margin: 0; padding-left: 1.2rem; display: grid; gap: .3rem; }
              .eyebrow { color: #356d4f; font-weight: 700; font-size: .85rem; text-transform: uppercase; letter-spacing: .04em; }
              .warning { padding: .75rem; border-radius: 12px; background: #fdf0e1; }
              .actions { display: flex; gap: .75rem; flex-wrap: wrap; }
              button { min-height: 2.8rem; padding: 0 18px; border-radius: 10px; font: inherit; font-weight: 600; cursor: pointer; border: 1px solid #356d4f; }
              .accept { background: #356d4f; color: #fffdf8; }
              .deny { background: transparent; color: #27523c; }
              button:focus-visible { outline: 3px solid #356d4f; outline-offset: 3px; }
            </style>
            </head>
            <body>
            <main>
              <p class="eyebrow">LifeOS</p>
              <h1>Autoriser {{E(clientName)}} à accéder à LifeOS&nbsp;?</h1>
              <p>Compte : <strong>{{E(email)}}</strong></p>
              <ul>
                <li>Il pourra lire et modifier tout ce que votre compte voit, y compris vos données de santé.</li>
                <li>Il agit seulement quand vous lui demandez, et vous pourrez retirer cet accès dans Réglages.</li>
              </ul>
              {{destination}}
              <form method="post" action="{{E(context.Request.PathBase + context.Request.Path)}}">
                {{hidden}}
                <div class="actions">
                  <button class="accept" type="submit" name="{{DecisionField}}" value="{{Accept}}">Autoriser</button>
                  <button class="deny" type="submit" name="{{DecisionField}}" value="{{Deny}}">Refuser</button>
                </div>
              </form>
            </main>
            </body>
            </html>
            """;

        // Never inside a frame: a hidden consent page could be clicked through (clickjacking).
        context.Response.Headers.XFrameOptions = "DENY";
        context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; frame-ancestors 'none'";
        context.Response.Headers.CacheControl = "no-store";
        return Results.Content(html, "text/html; charset=utf-8");
    }

    private static string E(string value) => WebUtility.HtmlEncode(value);
}
