using Azure.Identity;
using AutoRbac.Core.Models;

namespace AutoRbac.Core.Engine;

/// <summary>
/// Builds the probe execution context. Mirrors Resolve-RBACContext: by default the probe identity is
/// the ambient session (here, DefaultAzureCredential — Azure CLI / managed identity / env vars). The
/// live clients are only wired by <see cref="Live"/>; otherwise the context is offline and providers
/// evaluate against supplied assignments.
/// </summary>
public static class RbacContextFactory
{
    /// <summary>An offline, metadata-only context (no live Azure / REST calls).</summary>
    public static RbacContext Offline(string? tenantId = null) => new() { IsRunAs = false, TenantId = tenantId };

    /// <summary>
    /// A live context backed by DefaultAzureCredential. <paramref name="defaultScope"/> seeds the
    /// Azure role-assignment / definition queries (typically the subscription scope under test).
    /// </summary>
    public static RbacContext Live(string? tenantId = null, string? defaultScope = null)
    {
        var options = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrEmpty(tenantId)) options.TenantId = tenantId;
        var credential = new DefaultAzureCredential(options);

        return new RbacContext
        {
            IsRunAs = false,
            TenantId = tenantId,
            Azure = new AzureRbacClient(credential, defaultScope ?? "/"),
            Rest = new RestClient(credential),
        };
    }
}
