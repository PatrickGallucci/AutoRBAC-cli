using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Authorization;
using Azure.ResourceManager.Authorization.Models;
using AutoRbac.Core.Abstractions;
using AutoRbac.Core.Models;

namespace AutoRbac.Core.Engine;

/// <summary>
/// Live Azure (ARM) RBAC operations over Azure.ResourceManager — the C# equivalent of the
/// Get-AzRoleAssignment / Get-AzRoleDefinition / New-AzRoleAssignment paths in the PowerShell module.
///
/// Live mode treats <c>callerId</c> as a principal object id (the ARM API works on object ids, not
/// UPNs). The default query scope is the subscription; role-definition names are resolved from the
/// definitions visible at that scope.
/// </summary>
public sealed class AzureRbacClient : IAzureRbacClient
{
    private readonly ArmClient _client;
    private readonly TokenCredential _credential;
    private readonly ResourceIdentifier _defaultScope;
    private Dictionary<string, string>? _definitionIdToName;

    public AzureRbacClient(TokenCredential credential, string defaultScope)
    {
        _credential = credential;
        _client = new ArmClient(credential);
        _defaultScope = new ResourceIdentifier(string.IsNullOrEmpty(defaultScope) ? "/" : defaultScope);
    }

    public async Task<IReadOnlyList<AzureRoleAssignment>> GetRoleAssignmentsAsync(string callerId, CancellationToken ct = default)
    {
        var nameById = await DefinitionNamesAsync(ct).ConfigureAwait(false);
        var result = new List<AzureRoleAssignment>();

        var collection = _client.GetRoleAssignments(_defaultScope);
        await foreach (var ra in collection.GetAllAsync(cancellationToken: ct).ConfigureAwait(false))
        {
            var principalId = ra.Data.PrincipalId?.ToString();
            if (!string.Equals(principalId, callerId, StringComparison.OrdinalIgnoreCase)) continue;

            var defId = ra.Data.RoleDefinitionId?.ToString() ?? string.Empty;
            var roleName = nameById.TryGetValue(LastSegment(defId), out var n) ? n : defId;
            result.Add(new AzureRoleAssignment { RoleDefinitionName = roleName, Scope = ra.Data.Scope ?? _defaultScope.ToString() });
        }
        return result;
    }

    public async Task<IReadOnlyList<AzureRoleDefinition>> GetRoleDefinitionsAsync(CancellationToken ct = default)
    {
        var defs = new List<AzureRoleDefinition>();
        var collection = _client.GetAuthorizationRoleDefinitions(_defaultScope);
        await foreach (var def in collection.GetAllAsync(cancellationToken: ct).ConfigureAwait(false))
        {
            var actions = new List<string>();
            var notActions = new List<string>();
            foreach (var perm in def.Data.Permissions)
            {
                actions.AddRange(perm.Actions.Select(a => a.ToString()));
                notActions.AddRange(perm.NotActions.Select(a => a.ToString()));
            }
            defs.Add(new AzureRoleDefinition { Name = def.Data.RoleName, Actions = actions, NotActions = notActions });
        }
        return defs;
    }

    public async Task<bool> CreateRoleAssignmentAsync(string callerId, string roleName, string scope, CancellationToken ct = default)
    {
        var scopeId = new ResourceIdentifier(scope);
        var collection = _client.GetRoleAssignments(scopeId);

        // Resolve the role definition id for the friendly name at the target scope.
        var defCollection = _client.GetAuthorizationRoleDefinitions(scopeId);
        ResourceIdentifier? defId = null;
        await foreach (var def in defCollection.GetAllAsync(filter: $"roleName eq '{roleName}'", cancellationToken: ct).ConfigureAwait(false))
        {
            defId = def.Id;
            break;
        }
        if (defId is null)
        {
            throw new InvalidOperationException($"Role definition '{roleName}' not found at scope '{scope}'.");
        }

        // Idempotent: skip if the caller already holds the role at (exactly) this scope.
        await foreach (var ra in collection.GetAllAsync(filter: $"principalId eq '{callerId}'", cancellationToken: ct).ConfigureAwait(false))
        {
            if (string.Equals(LastSegment(ra.Data.RoleDefinitionId?.ToString() ?? ""), LastSegment(defId.ToString()), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        var content = new RoleAssignmentCreateOrUpdateContent(defId, Guid.Parse(callerId));
        await collection.CreateOrUpdateAsync(WaitUntil.Completed, Guid.NewGuid().ToString(), content, ct).ConfigureAwait(false);
        return true;
    }

    private async Task<Dictionary<string, string>> DefinitionNamesAsync(CancellationToken ct)
    {
        if (_definitionIdToName is not null) return _definitionIdToName;
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var collection = _client.GetAuthorizationRoleDefinitions(_defaultScope);
        await foreach (var def in collection.GetAllAsync(cancellationToken: ct).ConfigureAwait(false))
        {
            map[LastSegment(def.Id.ToString())] = def.Data.RoleName;
        }
        _definitionIdToName = map;
        return map;
    }

    private static string LastSegment(string resourceId)
    {
        var idx = resourceId.LastIndexOf('/');
        return idx < 0 ? resourceId : resourceId[(idx + 1)..];
    }
}
