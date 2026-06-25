using AutoRbac.Core.Models;

namespace AutoRbac.Core.Providers;

/// <summary>
/// A platform probe provider. The C# equivalent of the PSAutoRBAC provider contract (the
/// PSCustomObject with ResolveRequirement / TestAccess / ProbeLive / NewGrantScript / MatchScope
/// scriptblocks). Add a platform by implementing this interface and registering it.
/// </summary>
public interface IRbacProvider
{
    /// <summary>Canonical platform name (e.g. "Azure").</summary>
    string Name { get; }

    /// <summary>Accepted alternative names, matched case-insensitively.</summary>
    IReadOnlyList<string> Aliases { get; }

    /// <summary>Whether <see cref="ProbeLiveAsync"/> can derive a requirement from a live failure.</summary>
    bool SupportsLiveProbe { get; }

    /// <summary>Resolves the least-privilege requirement for a command (preflight, non-destructive).</summary>
    Task<Requirement> ResolveRequirementAsync(string command, RbacContext context, ProbeOptions options, CancellationToken ct = default);

    /// <summary>Tests whether the caller holds each required role at the scope (one state per role).</summary>
    Task<IReadOnlyList<AccessState>> TestAccessAsync(string callerId, IReadOnlyList<string> requiredRoles, string scope, RbacContext context, ProbeOptions options, CancellationToken ct = default);

    /// <summary>
    /// Derives a requirement from a live authorization failure. Only Azure can do this (only ARM names
    /// the missing action); other providers resolve authoritatively from the catalog / knowledge base.
    /// <paramref name="errorText"/> carries the AuthorizationFailed text to parse (Azure).
    /// </summary>
    Task<Requirement> ProbeLiveAsync(string command, string? errorText, string scope, RbacContext context, CancellationToken ct = default);

    /// <summary>Emits idempotent, platform-native grant + revoke snippets for a single role.</summary>
    GrantScript NewGrantScript(string callerId, string role, string scope, ProbeOptions options);

    /// <summary>Whether an assignment's scope covers the target scope.</summary>
    bool MatchScope(AzureRoleAssignment assignment, string scope);
}
