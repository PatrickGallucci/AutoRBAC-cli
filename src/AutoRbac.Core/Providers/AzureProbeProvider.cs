using AutoRbac.Core.Engine;
using AutoRbac.Core.Models;

namespace AutoRbac.Core.Providers;

/// <summary>
/// The Azure (ARM) RBAC probe provider. Azure is the only supported platform whose authorization
/// error reliably names the missing action and scope, so it is the only provider whose live probe can
/// derive a requirement from a failure. Preflight uses the offline knowledge base; access checks use
/// supplied assignments (offline) or the live Azure client.
/// </summary>
public sealed class AzureProbeProvider : ProviderBase
{
    public override string Name => "Azure";
    public override IReadOnlyList<string> Aliases => new[] { "Azure PowerShell", "Az", "ARM", "AzureRM", "Azure CLI" };
    public override bool SupportsLiveProbe => true;
    protected override string DataKey => "Azure PowerShell";

    public override bool MatchScope(AzureRoleAssignment assignment, string scope)
    {
        if (string.IsNullOrEmpty(assignment.Scope)) return false;
        var target = scope.TrimEnd('/');
        var held = assignment.Scope.TrimEnd('/');
        if (held == "/" || held.Length == 0) return true; // tenant root covers everything
        return target.StartsWith(held, StringComparison.OrdinalIgnoreCase);
    }

    public override async Task<IReadOnlyList<AccessState>> TestAccessAsync(
        string callerId, IReadOnlyList<string> requiredRoles, string scope, RbacContext context, ProbeOptions options, CancellationToken ct = default)
    {
        var scopeTrim = scope.TrimEnd('/');

        IReadOnlyList<AzureRoleAssignment> assignments;
        if (options.HasRoleAssignment)
        {
            // Offline: a supplied bare role with no scope is treated as held at the target scope.
            assignments = options.RoleAssignment!
                .Select(r => new AzureRoleAssignment { RoleDefinitionName = r.Role, Scope = r.Scope ?? scopeTrim })
                .ToList();
        }
        else if (context.Azure is not null)
        {
            assignments = await context.Azure.GetRoleAssignmentsAsync(callerId, ct).ConfigureAwait(false);
        }
        else
        {
            throw new InvalidOperationException(
                "No live Azure client and no supplied assignments. Use --live for an Azure SDK lookup, or pass --role-assignment for offline evaluation.");
        }

        var states = new List<AccessState>();
        foreach (var role in requiredRoles)
        {
            var has = assignments.Any(a =>
                string.Equals(a.RoleDefinitionName, role, StringComparison.OrdinalIgnoreCase) && MatchScope(a, scopeTrim));
            states.Add(new AccessState { Platform = Name, CallerId = callerId, Role = role, Scope = scopeTrim, HasAccess = has });
        }
        return states;
    }

    public override async Task<Requirement> ProbeLiveAsync(string command, string? errorText, string scope, RbacContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(errorText))
        {
            return new Requirement
            {
                Platform = Name, Command = command, Roles = Array.Empty<string>(), Permissions = Array.Empty<string>(),
                ScopeLevel = "Unknown",
                Notes = "No live authorization error was supplied (--error-text). Provide the AuthorizationFailed text from a real attempt to derive the exact action, or omit --live-probe for preflight.",
                IsKnown = true, Source = "LiveProbe",
            };
        }

        var parsed = AzAuthorizationErrorParser.Parse(errorText);
        if (!parsed.IsAuthorizationError)
        {
            throw new InvalidOperationException(
                "The supplied --error-text is not an Azure AuthorizationFailed error; nothing to derive. " +
                "Pass the text of a real authorization failure, or omit --live-probe.");
        }

        IReadOnlyList<AzureRoleDefinition>? liveDefs = null;
        if (context.Azure is not null)
        {
            liveDefs = await context.Azure.GetRoleDefinitionsAsync(ct).ConfigureAwait(false);
        }

        var match = RoleActionMapper.Map(parsed.Actions, liveDefs, KnowledgeBase.Default.RoleActionEntries);
        var derivedScope = parsed.Scopes.Count > 0 ? parsed.Scopes[0] : scope;

        return new Requirement
        {
            Platform = Name,
            Command = command,
            Roles = match.Roles,
            Permissions = parsed.Actions,
            ScopeLevel = "Resource",
            Notes = $"Derived from live AuthorizationFailed at scope '{derivedScope}' (role source: {match.Source}).",
            IsKnown = true,
            Source = "LiveProbe",
        };
    }

    public override GrantScript NewGrantScript(string callerId, string role, string scope, ProbeOptions options)
    {
        var s = scope.TrimEnd('/');
        var add = AddTemplate.Replace("__CALLER__", callerId).Replace("__ROLE__", role).Replace("__SCOPE__", s);
        var remove = RemoveTemplate.Replace("__CALLER__", callerId).Replace("__ROLE__", role).Replace("__SCOPE__", s);
        return new GrantScript { Platform = Name, CallerId = callerId, Role = role, Scope = s, AddScript = add, RemoveScript = remove };
    }

    private const string AddTemplate = @"# AutoRBAC: grant '__ROLE__' to '__CALLER__' at scope '__SCOPE__'.
# Run as an identity holding Microsoft.Authorization/roleAssignments/write
# (RBAC Administrator, User Access Administrator, or Owner). Idempotent.
Import-Module Az.Accounts -ErrorAction Stop
Import-Module Az.Resources -ErrorAction Stop
$upn = '__CALLER__'; $role = '__ROLE__'; $scope = '__SCOPE__'
$existing = Get-AzRoleAssignment -SignInName $upn -RoleDefinitionName $role -Scope $scope -ErrorAction SilentlyContinue
if ($existing) { Write-Host ""Already assigned '$role' to '$upn' at '$scope'."" }
else { New-AzRoleAssignment -SignInName $upn -RoleDefinitionName $role -Scope $scope; Write-Host ""Granted '$role'."" }
";

    private const string RemoveTemplate = @"# AutoRBAC: revoke '__ROLE__' from '__CALLER__' at scope '__SCOPE__'.
# Run as an identity holding Microsoft.Authorization/roleAssignments/delete. Idempotent.
Import-Module Az.Accounts -ErrorAction Stop
Import-Module Az.Resources -ErrorAction Stop
$upn = '__CALLER__'; $role = '__ROLE__'; $scope = '__SCOPE__'
$existing = Get-AzRoleAssignment -SignInName $upn -RoleDefinitionName $role -Scope $scope -ErrorAction SilentlyContinue
if ($existing) { Remove-AzRoleAssignment -SignInName $upn -RoleDefinitionName $role -Scope $scope; Write-Host ""Revoked '$role'."" }
else { Write-Host ""No '$role' assignment for '$upn' at '$scope'."" }
";
}
