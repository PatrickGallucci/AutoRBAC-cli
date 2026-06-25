using System.Text.Json;
using AutoRbac.Core.Models;

namespace AutoRbac.Core.Providers;

/// <summary>
/// The Microsoft Fabric RBAC probe provider. Fabric has no action-based RBAC model and its REST errors
/// do not name a required role, so error-parsing cannot derive a requirement. Access is governed by the
/// four workspace roles — Admin &gt; Member &gt; Contributor &gt; Viewer — where a higher role satisfies a
/// lower requirement. Requirements resolve from the knowledge base; access is evaluated from supplied
/// roles (offline) or by listing workspace role assignments via REST.
/// </summary>
public sealed class FabricProbeProvider : ProviderBase
{
    private const string ResourceUrl = "https://api.fabric.microsoft.com";
    private static readonly Dictionary<string, int> Rank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Viewer"] = 1, ["Contributor"] = 2, ["Member"] = 3, ["Admin"] = 4,
    };

    public override string Name => "Microsoft Fabric";
    public override IReadOnlyList<string> Aliases => new[] { "Fabric", "PowerBI", "Power BI" };
    public override bool SupportsLiveProbe => false;
    protected override string DataKey => "Microsoft Fabric";

    public override async Task<IReadOnlyList<AccessState>> TestAccessAsync(
        string callerId, IReadOnlyList<string> requiredRoles, string scope, RbacContext context, ProbeOptions options, CancellationToken ct = default)
    {
        var heldRoles = new List<string>();
        var evaluated = true;

        if (options.HasRoleAssignment)
        {
            heldRoles.AddRange(SuppliedRoles(options));
        }
        else if (!string.IsNullOrEmpty(scope) && scope != "/" && !scope.Equals("Tenant", StringComparison.OrdinalIgnoreCase) && context.Rest is not null)
        {
            var uri = $"{ResourceUrl}/v1/workspaces/{scope}/roleAssignments";
            var res = await context.Rest.SendAsync(ResourceUrl, uri, "GET", null, ct).ConfigureAwait(false);
            if (res.Success)
            {
                heldRoles.AddRange(ParseWorkspaceRoles(res.Content, callerId));
            }
            else if (res.IsAuthorizationError)
            {
                // Cannot even read assignments -> caller almost certainly lacks the role.
                heldRoles.Clear();
            }
            else
            {
                evaluated = false;
            }
        }
        else
        {
            evaluated = false;
        }

        var heldRank = heldRoles.Select(r => Rank.TryGetValue(r, out var v) ? v : 0).DefaultIfEmpty(0).Max();

        var states = new List<AccessState>();
        foreach (var role in requiredRoles)
        {
            var needRank = Rank.TryGetValue(role, out var nr) ? nr : 0;
            bool? has = !evaluated
                ? null
                : needRank == 0
                    ? heldRoles.Contains(role, StringComparer.OrdinalIgnoreCase)
                    : heldRank >= needRank;
            states.Add(new AccessState { Platform = Name, CallerId = callerId, Role = role, Scope = scope, HasAccess = has });
        }
        return states;
    }

    private static IEnumerable<string> ParseWorkspaceRoles(string? content, string callerId)
    {
        if (string.IsNullOrEmpty(content)) yield break;
        using var doc = JsonDocument.Parse(content);
        if (!doc.RootElement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array) yield break;

        foreach (var item in value.EnumerateArray())
        {
            if (!item.TryGetProperty("principal", out var principal)) continue;
            var id = principal.TryGetProperty("id", out var pid) ? pid.GetString() : null;
            string? upn = null;
            if (principal.TryGetProperty("userDetails", out var ud) && ud.TryGetProperty("userPrincipalName", out var u))
                upn = u.GetString();

            if (string.Equals(id, callerId, StringComparison.OrdinalIgnoreCase) || string.Equals(upn, callerId, StringComparison.OrdinalIgnoreCase))
            {
                if (item.TryGetProperty("role", out var role) && role.GetString() is { } r) yield return r;
            }
        }
    }

    public override async Task<Requirement> ProbeLiveAsync(string command, string? errorText, string scope, RbacContext context, CancellationToken ct = default)
    {
        var req = await ResolveRequirementAsync(command, context, new ProbeOptions(), ct).ConfigureAwait(false);
        return req with
        {
            Notes = "Fabric REST errors do not name the required role; requirement resolved from the knowledge base, not a live failure.",
        };
    }

    public override GrantScript NewGrantScript(string callerId, string role, string scope, ProbeOptions options)
    {
        var workspace = options.WorkspaceId ?? scope;
        var add = AddTemplate.Replace("__WORKSPACE__", workspace).Replace("__CALLER__", callerId).Replace("__ROLE__", role);
        var remove = RemoveTemplate.Replace("__WORKSPACE__", workspace).Replace("__CALLER__", callerId).Replace("__ROLE__", role);
        return new GrantScript { Platform = Name, CallerId = callerId, Role = role, Scope = scope.TrimEnd('/'), AddScript = add, RemoveScript = remove };
    }

    private const string AddTemplate = @"# AutoRBAC: grant Fabric workspace role '__ROLE__' to '__CALLER__' on workspace '__WORKSPACE__'.
# Run as a workspace Admin. Idempotent. CallerId must be the principal's object id.
$base = 'https://api.fabric.microsoft.com/v1/workspaces/__WORKSPACE__/roleAssignments'
$token = (Get-AzAccessToken -ResourceUrl 'https://api.fabric.microsoft.com').Token
$headers = @{ Authorization = ""Bearer $token"" }
$existing = (Invoke-RestMethod -Uri $base -Headers $headers).value |
    Where-Object { $_.principal.id -eq '__CALLER__' }
if ($existing) { Write-Host ""Already has '$($existing.role)' on workspace '__WORKSPACE__'."" }
else {
    $body = @{ principal = @{ id = '__CALLER__'; type = 'User' }; role = '__ROLE__' } | ConvertTo-Json
    Invoke-RestMethod -Uri $base -Method POST -Headers $headers -Body $body -ContentType 'application/json'
    Write-Host ""Granted '__ROLE__' to '__CALLER__' on workspace '__WORKSPACE__'.""
}
";

    private const string RemoveTemplate = @"# AutoRBAC: remove Fabric workspace role for '__CALLER__' from workspace '__WORKSPACE__'.
# Run as a workspace Admin. Idempotent.
$base = 'https://api.fabric.microsoft.com/v1/workspaces/__WORKSPACE__/roleAssignments'
$token = (Get-AzAccessToken -ResourceUrl 'https://api.fabric.microsoft.com').Token
$headers = @{ Authorization = ""Bearer $token"" }
$existing = (Invoke-RestMethod -Uri $base -Headers $headers).value |
    Where-Object { $_.principal.id -eq '__CALLER__' }
if ($existing) {
    Invoke-RestMethod -Uri ""$base/$($existing.id)"" -Method DELETE -Headers $headers
    Write-Host ""Removed workspace role from '__CALLER__'.""
} else { Write-Host ""No workspace role for '__CALLER__' on '__WORKSPACE__'."" }
";
}
