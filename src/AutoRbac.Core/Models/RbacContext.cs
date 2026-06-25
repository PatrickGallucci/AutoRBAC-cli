using AutoRbac.Core.Abstractions;

namespace AutoRbac.Core.Models;

/// <summary>
/// The probe execution context: the identity the checks/grants run *as*, plus the live clients used
/// to talk to Azure and the data-plane REST APIs. Mirrors PSAutoRBAC.Context.
///
/// PSAutoRBAC separates two identities: the *probe identity* (who runs the checks, modelled here) and
/// the *target caller* (whose access is being evaluated, passed separately as callerId). When the live
/// clients are null the context is offline/metadata-only and providers evaluate against supplied
/// assignments.
/// </summary>
public sealed class RbacContext : IDisposable
{
    public bool IsRunAs { get; init; }
    public string? TenantId { get; init; }
    public IAzureRbacClient? Azure { get; init; }
    public IRestClient? Rest { get; init; }

    private readonly Action? _disconnect;

    public RbacContext(Action? disconnect = null) => _disconnect = disconnect;

    /// <summary>A purely offline context with no live clients (the default for tests / dry-runs).</summary>
    public static RbacContext Offline() => new();

    public void Dispose() => _disconnect?.Invoke();
}
