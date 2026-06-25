namespace AutoRbac.Core.Models;

/// <summary>How a requirement was resolved on the probe.</summary>
public enum ProbeMode
{
    Preflight,
    LiveProbe,
}

/// <summary>
/// The minimum RBAC roles / permissions a command requires on a platform.
/// Mirrors the PSAutoRBAC.Requirement object.
/// </summary>
public sealed record Requirement
{
    public required string Platform { get; init; }
    public required string Command { get; init; }
    /// <summary>The grantable role name(s).</summary>
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    /// <summary>Finer-grained detail: ARM actions (Azure), Graph API scopes (Graph), or notes.</summary>
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
    public string? ScopeLevel { get; init; }
    public string? Notes { get; init; }
    /// <summary>False when the command was not in the knowledge base and the platform default was used.</summary>
    public bool IsKnown { get; init; }
    public string Source { get; init; } = "KnowledgeBase";
}

/// <summary>The verdict for a single required role at a scope. Mirrors PSAutoRBAC.AccessState.</summary>
public sealed record AccessState
{
    public required string Platform { get; init; }
    public required string CallerId { get; init; }
    public required string Role { get; init; }
    public string? Scope { get; init; }
    /// <summary>True/false when evaluated; null when the provider could not evaluate access.</summary>
    public bool? HasAccess { get; init; }
}

/// <summary>Idempotent grant/revoke snippets for a role on a platform. Mirrors PSAutoRBAC.GrantScript.</summary>
public sealed record GrantScript
{
    public required string Platform { get; init; }
    public required string CallerId { get; init; }
    public required string Role { get; init; }
    public required string Scope { get; init; }
    public string AddScript { get; init; } = string.Empty;
    public string RemoveScript { get; init; } = string.Empty;
}

/// <summary>Combined probe result, one per required role. Mirrors PSAutoRBAC.ProbeResult.</summary>
public sealed record ProbeResult
{
    public required string Platform { get; init; }
    public required string Command { get; init; }
    public required string CallerId { get; init; }
    public string? Scope { get; init; }
    public string? Role { get; init; }
    public bool? HasAccess { get; init; }
    public ProbeMode Mode { get; init; }
    public bool IsKnown { get; init; }
    public string? Source { get; init; }
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
    public string? Notes { get; init; }
    public string? AddScript { get; init; }
    public string? RemoveScript { get; init; }
}

/// <summary>End-to-end orchestration result, one per required role. Mirrors PSAutoRBAC.AccessResult.</summary>
public sealed record AccessResult
{
    public required string Platform { get; init; }
    public required string Command { get; init; }
    public required string CallerId { get; init; }
    public string? Scope { get; init; }
    public required string Role { get; init; }
    public bool? HasAccess { get; init; }
    /// <summary>True when a missing role was actually granted (Azure only).</summary>
    public bool Applied { get; init; }
    public bool IsKnown { get; init; }
    public string? Source { get; init; }
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
    public string? Notes { get; init; }
    public string? AddScript { get; init; }
    public string? RemoveScript { get; init; }
}

/// <summary>Introspection record for a registered provider. Mirrors PSAutoRBAC.ProviderInfo.</summary>
public sealed record ProviderInfo
{
    public required string Name { get; init; }
    public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();
    public bool SupportsLiveProbe { get; init; }
}

/// <summary>Result of parsing an ARM AuthorizationFailed error. Mirrors PSAutoRBAC.ParsedError.</summary>
public sealed record ParsedError
{
    public IReadOnlyList<string> Actions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Scopes { get; init; } = Array.Empty<string>();
    public bool IsAuthorizationError { get; init; }
    public string Message { get; init; } = string.Empty;
}

/// <summary>Result of mapping action(s) to candidate role(s). Mirrors PSAutoRBAC.RoleMatch.</summary>
public sealed record RoleMatch
{
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    public string Source { get; init; } = "None";
    public IReadOnlyList<string> Actions { get; init; } = Array.Empty<string>();
}

/// <summary>Normalized REST result for the data-plane providers. Mirrors PSAutoRBAC.RestResult.</summary>
public sealed record RestResult
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public bool IsAuthorizationError { get; init; }
    public string? Content { get; init; }
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// A pre-supplied role assignment for fully offline access evaluation. Accepts either a bare role
/// name (e.g. "Admin") or a role + scope pair (e.g. Reader at /subscriptions/SUB1).
/// </summary>
public sealed record RoleAssignmentInput
{
    public required string Role { get; init; }
    public string? Scope { get; init; }

    /// <summary>Parse "Role@Scope" or just "Role" from the CLI.</summary>
    public static RoleAssignmentInput Parse(string value)
    {
        var at = value.IndexOf('@');
        return at < 0
            ? new RoleAssignmentInput { Role = value.Trim() }
            : new RoleAssignmentInput { Role = value[..at].Trim(), Scope = value[(at + 1)..].Trim() };
    }
}
