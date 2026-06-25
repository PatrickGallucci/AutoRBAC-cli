using AutoRbac.Core.Engine;
using AutoRbac.Core.Models;
using AutoRbac.Core.Providers;

namespace AutoRbac.Core;

/// <summary>
/// The public AutoRBAC surface — the C# equivalent of the six exported PSAutoRBAC cmdlets. Dispatches
/// to the registered platform providers and composes their results.
/// </summary>
public sealed class RbacService
{
    private readonly ProviderRegistry _registry;

    public RbacService(ProviderRegistry? registry = null) => _registry = registry ?? ProviderRegistry.CreateDefault();

    /// <summary>Get-RBACProvider: list the registered providers (optionally resolving one).</summary>
    public IReadOnlyList<ProviderInfo> GetProviders(string? platform = null)
    {
        if (string.IsNullOrEmpty(platform)) return _registry.List();
        var p = _registry.Resolve(platform);
        return new[] { new ProviderInfo { Name = p.Name, Aliases = p.Aliases, SupportsLiveProbe = p.SupportsLiveProbe } };
    }

    /// <summary>Get-RBACRequirement: the minimum role(s) / permissions a command needs.</summary>
    public Task<Requirement> GetRequirementAsync(string platform, string command, RbacContext context, ProbeOptions options, CancellationToken ct = default)
    {
        var provider = _registry.Resolve(platform);
        return provider.ResolveRequirementAsync(command, context, options, ct);
    }

    /// <summary>Test-RBACAccess: whether the caller holds the required role(s) at a scope.</summary>
    public Task<IReadOnlyList<AccessState>> TestAccessAsync(
        string platform, string callerId, IReadOnlyList<string> requiredRoles, string scope, RbacContext context, ProbeOptions options, CancellationToken ct = default)
    {
        var provider = _registry.Resolve(platform);
        return provider.TestAccessAsync(callerId, requiredRoles, scope, context, options, ct);
    }

    /// <summary>New-RBACGrantScript: idempotent grant + revoke snippets for a role.</summary>
    public GrantScript NewGrantScript(string platform, string callerId, string role, string scope, ProbeOptions options)
    {
        var provider = _registry.Resolve(platform);
        return provider.NewGrantScript(callerId, role, scope, options);
    }

    /// <summary>Invoke-RBACProbe: resolve the requirement (preflight or live) and the access verdict.</summary>
    public async Task<IReadOnlyList<ProbeResult>> ProbeAsync(
        string platform, string command, string callerId, ScopeInputs scopeInputs, ProbeOptions options, RbacContext context,
        bool liveProbe = false, string? errorText = null, Action<string>? onWarning = null, CancellationToken ct = default)
    {
        var provider = _registry.Resolve(platform);
        var scope = ScopeResolver.Resolve(scopeInputs, allowTenantRoot: true);

        var mode = ProbeMode.Preflight;
        Requirement requirement;
        if (liveProbe && !provider.SupportsLiveProbe)
        {
            onWarning?.Invoke($"Platform '{provider.Name}' does not support live probing (its authorization error names no permission). Falling back to preflight resolution.");
            requirement = await provider.ResolveRequirementAsync(command, context, options, ct).ConfigureAwait(false);
        }
        else if (liveProbe)
        {
            requirement = await provider.ProbeLiveAsync(command, errorText, scope, context, ct).ConfigureAwait(false);
            mode = ProbeMode.LiveProbe;
        }
        else
        {
            requirement = await provider.ResolveRequirementAsync(command, context, options, ct).ConfigureAwait(false);
        }

        var roles = requirement.Roles.Where(r => !string.IsNullOrEmpty(r)).ToList();
        var states = roles.Count > 0
            ? await provider.TestAccessAsync(callerId, roles, scope, context, options, ct).ConfigureAwait(false)
            : Array.Empty<AccessState>();

        if (states.Count == 0)
        {
            return new[]
            {
                new ProbeResult
                {
                    Platform = provider.Name, Command = command, CallerId = callerId, Scope = scope,
                    Role = null, HasAccess = null, Mode = mode, IsKnown = requirement.IsKnown, Source = requirement.Source,
                    Permissions = requirement.Permissions, Notes = requirement.Notes, AddScript = null, RemoveScript = null,
                },
            };
        }

        var results = new List<ProbeResult>();
        foreach (var state in states)
        {
            var scripts = provider.NewGrantScript(callerId, state.Role, scope, options);
            results.Add(new ProbeResult
            {
                Platform = provider.Name, Command = command, CallerId = callerId, Scope = scope,
                Role = state.Role, HasAccess = state.HasAccess, Mode = mode, IsKnown = requirement.IsKnown,
                Source = requirement.Source, Permissions = requirement.Permissions, Notes = requirement.Notes,
                AddScript = scripts.AddScript, RemoveScript = scripts.RemoveScript,
            });
        }
        return results;
    }

    /// <summary>Set-RBACAccess: discover, evaluate, and (with apply) grant the missing least-privilege roles.</summary>
    public async Task<IReadOnlyList<AccessResult>> SetAccessAsync(
        string platform, string command, string callerId, ScopeInputs scopeInputs, ProbeOptions options, RbacContext context,
        bool apply = false, bool liveProbe = false, string? errorText = null, Action<string>? onWarning = null, CancellationToken ct = default)
    {
        var provider = _registry.Resolve(platform);
        var scope = ScopeResolver.Resolve(scopeInputs, allowTenantRoot: true);

        Requirement requirement;
        if (liveProbe && provider.SupportsLiveProbe)
        {
            requirement = await provider.ProbeLiveAsync(command, errorText, scope, context, ct).ConfigureAwait(false);
        }
        else
        {
            requirement = await provider.ResolveRequirementAsync(command, context, options, ct).ConfigureAwait(false);
        }

        var roles = requirement.Roles.Where(r => !string.IsNullOrEmpty(r)).ToList();
        var states = roles.Count > 0
            ? await provider.TestAccessAsync(callerId, roles, scope, context, options, ct).ConfigureAwait(false)
            : Array.Empty<AccessState>();

        var results = new List<AccessResult>();
        foreach (var state in states)
        {
            var scripts = provider.NewGrantScript(callerId, state.Role, scope, options);
            var applied = false;

            if (state.HasAccess != true && apply)
            {
                if (provider.Name == "Azure" && context.Azure is not null)
                {
                    applied = await context.Azure.CreateRoleAssignmentAsync(callerId, state.Role, scope, ct).ConfigureAwait(false);
                }
                else
                {
                    onWarning?.Invoke($"Automatic grant is not performed for '{provider.Name}'. Run the AddScript on the result from an identity with the necessary management permission.");
                }
            }

            results.Add(new AccessResult
            {
                Platform = provider.Name, Command = command, CallerId = callerId, Scope = scope,
                Role = state.Role, HasAccess = state.HasAccess, Applied = applied, IsKnown = requirement.IsKnown,
                Source = requirement.Source, Permissions = requirement.Permissions, Notes = requirement.Notes,
                AddScript = scripts.AddScript, RemoveScript = scripts.RemoveScript,
            });
        }
        return results;
    }
}
