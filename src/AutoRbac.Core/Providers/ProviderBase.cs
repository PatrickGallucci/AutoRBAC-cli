using AutoRbac.Core.Engine;
using AutoRbac.Core.Models;

namespace AutoRbac.Core.Providers;

/// <summary>
/// Shared provider behaviour: knowledge-base requirement resolution and the offline role list helper.
/// Concrete providers supply their platform name, the data key they read in the command map, and the
/// platform-specific TestAccess / ProbeLive / NewGrantScript behaviour.
/// </summary>
public abstract class ProviderBase : IRbacProvider
{
    public abstract string Name { get; }
    public abstract IReadOnlyList<string> Aliases { get; }
    public abstract bool SupportsLiveProbe { get; }

    /// <summary>The platform key this provider reads in CommandRoleMap (e.g. "Azure PowerShell").</summary>
    protected abstract string DataKey { get; }

    protected static KnowledgeBase Kb(ProbeOptions options) =>
        string.IsNullOrEmpty(options.MapPath) ? KnowledgeBase.Default : KnowledgeBase.FromPath(options.MapPath);

    public virtual Task<Requirement> ResolveRequirementAsync(string command, RbacContext context, ProbeOptions options, CancellationToken ct = default)
    {
        var (entry, isKnown) = Kb(options).ResolveCommand(DataKey, command);
        return Task.FromResult(new Requirement
        {
            Platform = Name,
            Command = command,
            Roles = entry.Roles.ToList(),
            Permissions = entry.Actions.ToList(),
            ScopeLevel = entry.ScopeLevel,
            Notes = entry.Notes,
            IsKnown = isKnown,
            Source = "KnowledgeBase",
        });
    }

    public abstract Task<IReadOnlyList<AccessState>> TestAccessAsync(string callerId, IReadOnlyList<string> requiredRoles, string scope, RbacContext context, ProbeOptions options, CancellationToken ct = default);

    public abstract Task<Requirement> ProbeLiveAsync(string command, string? errorText, string scope, RbacContext context, CancellationToken ct = default);

    public abstract GrantScript NewGrantScript(string callerId, string role, string scope, ProbeOptions options);

    public virtual bool MatchScope(AzureRoleAssignment assignment, string scope) => true;

    /// <summary>Bare role names supplied for offline evaluation (handles both "Role" and "Role@Scope").</summary>
    protected static IReadOnlyList<string> SuppliedRoles(ProbeOptions options) =>
        options.RoleAssignment?.Select(r => r.Role).ToList() ?? new List<string>();
}
