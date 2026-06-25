using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Core;
using AutoRbac.Core.Abstractions;
using AutoRbac.Core.Models;

namespace AutoRbac.Core.Engine;

/// <summary>
/// Authenticated REST client for the data-plane providers (Fabric / Purview). Direct port of
/// Invoke-RBACRestRequest: acquires a bearer token for the target resource from the probe identity's
/// credential and performs the call, returning a normalized result that distinguishes success,
/// authorization failure (401/403), and other errors — without throwing on auth failures.
/// </summary>
public sealed class RestClient : IRestClient
{
    private static readonly HttpClient Http = new();
    private readonly TokenCredential _credential;

    public RestClient(TokenCredential credential) => _credential = credential;

    public async Task<RestResult> SendAsync(string resourceUrl, string uri, string method = "GET", object? body = null, CancellationToken ct = default)
    {
        string token;
        try
        {
            var scope = resourceUrl.TrimEnd('/') + "/.default";
            var accessToken = await _credential.GetTokenAsync(new TokenRequestContext(new[] { scope }), ct).ConfigureAwait(false);
            token = accessToken.Token;
        }
        catch (Exception ex)
        {
            return new RestResult { Success = false, StatusCode = 0, IsAuthorizationError = false, Content = null, Message = $"Token acquisition failed: {ex.Message}" };
        }

        using var request = new HttpRequestMessage(new HttpMethod(method), uri);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }

        try
        {
            using var response = await Http.SendAsync(request, ct).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return new RestResult { Success = true, StatusCode = (int)response.StatusCode, IsAuthorizationError = false, Content = content, Message = string.Empty };
            }
            var isAuth = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
            return new RestResult { Success = false, StatusCode = (int)response.StatusCode, IsAuthorizationError = isAuth, Content = content, Message = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}" };
        }
        catch (Exception ex)
        {
            var isAuth = ex.Message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("Forbidden", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("InsufficientPrivileges", StringComparison.OrdinalIgnoreCase);
            return new RestResult { Success = false, StatusCode = 0, IsAuthorizationError = isAuth, Content = null, Message = ex.Message };
        }
    }
}
