using AutoRbac.Core.Models;

namespace AutoRbac.Core.Abstractions;

/// <summary>
/// Issues an authenticated REST request for the data-plane providers (Fabric / Purview), acquiring
/// a bearer token for the target resource. Returns a normalized result that distinguishes success,
/// authorization failure (401/403), and other errors without throwing on auth failures. Left null
/// on the context for offline evaluation.
/// </summary>
public interface IRestClient
{
    Task<RestResult> SendAsync(
        string resourceUrl,
        string uri,
        string method = "GET",
        object? body = null,
        CancellationToken ct = default);
}
