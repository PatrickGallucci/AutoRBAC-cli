namespace AutoRbac.Core.Engine;

/// <summary>Inputs that can build (or override) an Azure RBAC scope. Mirrors the scope parameters.</summary>
public sealed class ScopeInputs
{
    public string? Scope { get; set; }
    public string? ManagementGroupId { get; set; }
    public string? SubscriptionId { get; set; }
    public string? ResourceGroupName { get; set; }
    public string? ResourceId { get; set; }
}

/// <summary>
/// Normalizes or builds an Azure RBAC scope string. Direct port of Resolve-RBACScope: an explicit
/// scope always wins, a trailing slash is trimmed, and for tenant-level platforms '/' is allowed.
/// </summary>
public static class ScopeResolver
{
    public static string Resolve(ScopeInputs inputs, bool allowTenantRoot = false)
    {
        if (!string.IsNullOrEmpty(inputs.Scope))
        {
            var trimmed = inputs.Scope.TrimEnd('/');
            return string.IsNullOrEmpty(trimmed) ? "/" : trimmed;
        }

        if (!string.IsNullOrEmpty(inputs.ResourceId)) return inputs.ResourceId.TrimEnd('/');

        if (!string.IsNullOrEmpty(inputs.ManagementGroupId))
            return $"/providers/Microsoft.Management/managementGroups/{inputs.ManagementGroupId}";

        if (!string.IsNullOrEmpty(inputs.SubscriptionId) && !string.IsNullOrEmpty(inputs.ResourceGroupName))
            return $"/subscriptions/{inputs.SubscriptionId}/resourceGroups/{inputs.ResourceGroupName}";

        if (!string.IsNullOrEmpty(inputs.SubscriptionId))
            return $"/subscriptions/{inputs.SubscriptionId}";

        if (allowTenantRoot) return "/";

        throw new InvalidOperationException(
            "Unable to resolve an RBAC scope. Provide --scope, --management-group-id, or --subscription-id " +
            "(optionally with --resource-group-name / --resource-id).");
    }
}
