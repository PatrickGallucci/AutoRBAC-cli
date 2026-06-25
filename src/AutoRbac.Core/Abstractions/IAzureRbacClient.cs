using AutoRbac.Core.Models;

namespace AutoRbac.Core.Abstractions;

/// <summary>
/// Live Azure (ARM) RBAC operations, the C# equivalent of Get-AzRoleAssignment /
/// Get-AzRoleDefinition / New-AzRoleAssignment. Implemented over Azure.ResourceManager; left null
/// on the context for fully offline evaluation, in which case providers use supplied assignments.
/// </summary>
public interface IAzureRbacClient
{
    /// <summary>Role assignments held by the caller (by sign-in name or object id), across scopes.</summary>
    Task<IReadOnlyList<AzureRoleAssignment>> GetRoleAssignmentsAsync(string callerId, CancellationToken ct = default);

    /// <summary>All role definitions visible to the probe identity (for action-to-role mapping).</summary>
    Task<IReadOnlyList<AzureRoleDefinition>> GetRoleDefinitionsAsync(CancellationToken ct = default);

    /// <summary>Creates a role assignment (the live grant). Idempotent: returns false if it already exists.</summary>
    Task<bool> CreateRoleAssignmentAsync(string callerId, string roleName, string scope, CancellationToken ct = default);
}
