using AutoRbac.Core.Models;

namespace AutoRbac.Core.Providers;

/// <summary>
/// The Microsoft Graph (Entra) RBAC probe provider. Graph's authorization error
/// ("Authorization_RequestDenied. Insufficient privileges...") does NOT name the missing permission,
/// so error-parsing is useless; requirements resolve from the knowledge base. (The PowerShell module
/// could additionally consult Find-MgGraphCommand, a PowerShell-only catalog with no SDK equivalent.)
/// Access is evaluated against supplied consented scopes / directory roles.
/// </summary>
public sealed class GraphProbeProvider : ProviderBase
{
    public override string Name => "Microsoft Graph";
    public override IReadOnlyList<string> Aliases => new[] { "Graph", "Entra", "MgGraph", "AzureAD", "Microsoft Entra" };
    public override bool SupportsLiveProbe => false;
    protected override string DataKey => "Microsoft Graph";

    public override Task<IReadOnlyList<AccessState>> TestAccessAsync(
        string callerId, IReadOnlyList<string> requiredRoles, string scope, RbacContext context, ProbeOptions options, CancellationToken ct = default)
    {
        // Offline / explicit: the caller passes the granted scopes or directory roles.
        IReadOnlyList<string>? granted = options.HasRoleAssignment ? SuppliedRoles(options) : null;

        var states = requiredRoles.Select(role => new AccessState
        {
            Platform = Name,
            CallerId = callerId,
            Role = role,
            Scope = "Tenant",
            HasAccess = granted is not null && granted.Contains(role, StringComparer.OrdinalIgnoreCase),
        }).ToList();

        return Task.FromResult<IReadOnlyList<AccessState>>(states);
    }

    public override async Task<Requirement> ProbeLiveAsync(string command, string? errorText, string scope, RbacContext context, CancellationToken ct = default)
    {
        // Graph errors are non-diagnostic; resolve authoritatively from the knowledge base instead.
        var req = await ResolveRequirementAsync(command, context, new ProbeOptions(), ct).ConfigureAwait(false);
        return req with
        {
            Notes = "Graph authorization errors do not name the missing permission; requirement resolved from the knowledge base rather than a live failure.",
        };
    }

    public override GrantScript NewGrantScript(string callerId, string role, string scope, ProbeOptions options)
    {
        var add = AddTemplate.Replace("__CALLER__", callerId).Replace("__ROLE__", role);
        var remove = RemoveTemplate.Replace("__CALLER__", callerId).Replace("__ROLE__", role);
        return new GrantScript { Platform = Name, CallerId = callerId, Role = role, Scope = scope.TrimEnd('/'), AddScript = add, RemoveScript = remove };
    }

    private const string AddTemplate = @"# AutoRBAC: assign Entra directory role '__ROLE__' to '__CALLER__'.
# Run as Privileged Role Administrator (or Global Administrator). Idempotent.
Import-Module Microsoft.Graph.Identity.DirectoryManagement -ErrorAction Stop
Connect-MgGraph -Scopes 'RoleManagement.ReadWrite.Directory' -NoWelcome
$user = Get-MgUser -UserId '__CALLER__' -ErrorAction Stop
$def  = Get-MgRoleManagementDirectoryRoleDefinition -Filter ""displayName eq '__ROLE__'"" | Select-Object -First 1
if (-not $def) { throw ""Directory role '__ROLE__' not found."" }
$held = Get-MgRoleManagementDirectoryRoleAssignment -Filter ""principalId eq '$($user.Id)' and roleDefinitionId eq '$($def.Id)'"" -ErrorAction SilentlyContinue
if ($held) { Write-Host ""Already assigned '__ROLE__' to '__CALLER__'."" }
else {
    New-MgRoleManagementDirectoryRoleAssignment -PrincipalId $user.Id -RoleDefinitionId $def.Id -DirectoryScopeId '/'
    Write-Host ""Assigned '__ROLE__' to '__CALLER__'.""
}
";

    private const string RemoveTemplate = @"# AutoRBAC: remove Entra directory role '__ROLE__' from '__CALLER__'.
# Run as Privileged Role Administrator (or Global Administrator). Idempotent.
Import-Module Microsoft.Graph.Identity.DirectoryManagement -ErrorAction Stop
Connect-MgGraph -Scopes 'RoleManagement.ReadWrite.Directory' -NoWelcome
$user = Get-MgUser -UserId '__CALLER__' -ErrorAction Stop
$def  = Get-MgRoleManagementDirectoryRoleDefinition -Filter ""displayName eq '__ROLE__'"" | Select-Object -First 1
if (-not $def) { throw ""Directory role '__ROLE__' not found."" }
$held = Get-MgRoleManagementDirectoryRoleAssignment -Filter ""principalId eq '$($user.Id)' and roleDefinitionId eq '$($def.Id)'"" -ErrorAction SilentlyContinue
if ($held) { Remove-MgRoleManagementDirectoryRoleAssignment -UnifiedRoleAssignmentId $held.Id; Write-Host ""Removed '__ROLE__'."" }
else { Write-Host ""No '__ROLE__' assignment for '__CALLER__'."" }
";
}
