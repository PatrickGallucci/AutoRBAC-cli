using AutoRbac.Core.Models;

namespace AutoRbac.Core.Providers;

/// <summary>
/// Indexes the platform providers by canonical name and every alias (case-insensitively). Mirrors the
/// PSAutoRBAC provider registry (Register-RBACProvider / Get-RBACProviderInternal). The four built-in
/// providers are registered by <see cref="CreateDefault"/>.
/// </summary>
public sealed class ProviderRegistry
{
    private readonly Dictionary<string, IRbacProvider> _byKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IRbacProvider> _providers = new();

    /// <summary>A registry with the four built-in providers.</summary>
    public static ProviderRegistry CreateDefault()
    {
        var registry = new ProviderRegistry();
        registry.Register(new AzureProbeProvider());
        registry.Register(new GraphProbeProvider());
        registry.Register(new FabricProbeProvider());
        registry.Register(new PurviewProbeProvider());
        return registry;
    }

    public void Register(IRbacProvider provider)
    {
        _providers.Add(provider);
        foreach (var key in new[] { provider.Name }.Concat(provider.Aliases))
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            _byKey[key] = provider;
        }
    }

    /// <summary>Resolves a provider by name or alias; throws listing known platforms when none matches.</summary>
    public IRbacProvider Resolve(string platform)
    {
        if (_byKey.TryGetValue(platform.Trim(), out var provider))
        {
            return provider;
        }
        var known = string.Join(", ", _providers.Select(p => p.Name).Distinct().OrderBy(n => n));
        throw new InvalidOperationException($"Unknown platform '{platform}'. Known platforms: {known}.");
    }

    /// <summary>The distinct registered providers.</summary>
    public IReadOnlyList<IRbacProvider> Providers => _providers;

    /// <summary>Introspection records for the public provider listing.</summary>
    public IReadOnlyList<ProviderInfo> List() => _providers
        .OrderBy(p => p.Name)
        .Select(p => new ProviderInfo { Name = p.Name, Aliases = p.Aliases, SupportsLiveProbe = p.SupportsLiveProbe })
        .ToList();
}
