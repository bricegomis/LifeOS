using System.Text.RegularExpressions;
using LifeOS.Domain.Common;

namespace LifeOS.Domain.Households;

/// <summary>
/// The state of one account's Garmin connection, shared by the garmin-sync service and the
/// screens (ADR 0019). The service reports what it does; the user asks for a sync, a new connection,
/// or gives the code Garmin sent. A code lives 15 minutes and is handed to the service only once.
/// LifeOS never calls Garmin itself.
/// </summary>
public sealed partial class GarminLink : Entity
{
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);

    /// <summary>never, ok, mfa_requested, code_refused, code_expired, credentials_refused or error.</summary>
    public static readonly IReadOnlyDictionary<string, string> States = new Dictionary<string, string>
    {
        ["never"] = "Jamais synchronisé",
        ["ok"] = "Connecté",
        ["mfa_requested"] = "Code Garmin demandé",
        ["code_refused"] = "Code refusé",
        ["code_expired"] = "Code expiré",
        ["credentials_refused"] = "Identifiants Garmin refusés",
        ["error"] = "Connexion Garmin en panne",
    };

    public Guid HouseholdId { get; private set; }
    public Guid UserId { get; private set; }
    public string State { get; private set; } = "never";
    public string? Message { get; private set; }
    public DateTimeOffset? LastSeenAt { get; private set; }
    public DateTimeOffset? LastSuccessAt { get; private set; }
    public string? LastSuccessSummary { get; private set; }
    public DateTimeOffset? LastErrorAt { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset? MfaRequestedAt { get; private set; }
    public DateTimeOffset? SyncRequestedAt { get; private set; }
    public DateTimeOffset? ReconnectRequestedAt { get; private set; }
    public string? MfaCode { get; private set; }
    public DateTimeOffset? MfaCodeAt { get; private set; }
    public DateTimeOffset? NextSyncAt { get; private set; }

    private GarminLink() { }

    public static GarminLink Create(Guid householdId, Guid userId)
    {
        if (householdId == Guid.Empty || userId == Guid.Empty) throw new ArgumentException("Foyer et compte obligatoires.");
        return new GarminLink { Id = Guid.NewGuid(), HouseholdId = householdId, UserId = userId };
    }

    /// <summary>The state as shown: a code request left unanswered for 15 minutes is expired.</summary>
    public string StateAt(DateTimeOffset now) =>
        State == "mfa_requested" && MfaRequestedAt is { } asked && now - asked > CodeLifetime ? "code_expired" : State;

    public bool WaitsForCode(DateTimeOffset now) => StateAt(now) is "mfa_requested" or "code_refused";

    public void RequestSync(DateTimeOffset now) => SyncRequestedAt = now;

    public void RequestReconnect(DateTimeOffset now)
    {
        ReconnectRequestedAt = now;
        MfaCode = null;
        MfaCodeAt = null;
    }

    public void SubmitCode(string? code, DateTimeOffset now)
    {
        var digits = (code ?? "").Replace(" ", "");
        if (!CodeFormat().IsMatch(digits)) throw new ArgumentException("Le code Garmin fait 6 chiffres.");
        if (!WaitsForCode(now)) throw new ArgumentException("Garmin n'attend pas de code pour l'instant. Relancez la connexion.");
        MfaCode = digits;
        MfaCodeAt = now;
    }

    /// <summary>What the service must do now; the requests and the code are handed over once.</summary>
    public GarminCommands TakeCommands(DateTimeOffset now)
    {
        var code = MfaCode is not null && MfaCodeAt is { } at && now - at <= CodeLifetime ? MfaCode : null;
        var commands = new GarminCommands(SyncRequestedAt is not null, ReconnectRequestedAt is not null, code);
        SyncRequestedAt = null;
        ReconnectRequestedAt = null;
        MfaCode = null;
        MfaCodeAt = null;
        LastSeenAt = now;
        return commands;
    }

    /// <summary>What the service reports after a step.</summary>
    public void Report(string state, string? message, string? summary, DateTimeOffset? nextSyncAt, DateTimeOffset now)
    {
        if (!States.ContainsKey(state) || state == "never") throw new ArgumentException("État Garmin inconnu.");
        var text = Trim(message, 1000);
        if (state == "mfa_requested" && State != "mfa_requested") MfaRequestedAt = now;
        if (state is not ("mfa_requested" or "code_refused")) MfaRequestedAt = null;
        if (state == "ok" && summary is not null)
        {
            LastSuccessAt = now;
            LastSuccessSummary = Trim(summary, 500);
        }
        if (state is "error" or "credentials_refused")
        {
            LastErrorAt = now;
            LastError = text;
        }
        State = state;
        Message = text;
        NextSyncAt = nextSyncAt;
        LastSeenAt = now;
    }

    private static string? Trim(string? text, int max) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim()[..Math.Min(max, text.Trim().Length)];

    [GeneratedRegex("^[0-9]{6}$")]
    private static partial Regex CodeFormat();
}

public sealed record GarminCommands(bool Sync, bool Reconnect, string? MfaCode);
