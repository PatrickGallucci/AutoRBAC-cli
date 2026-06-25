using AutoRbac.Core.Abstractions;
using AutoRbac.Core.Models;

namespace AutoRbac.Tests;

/// <summary>
/// In-memory Azure RBAC client for the live-path tests, the C# equivalent of the global Az cmdlet
/// stubs in the Pester suite (Get-AzRoleAssignment / Get-AzRoleDefinition / New-AzRoleAssignment).
/// </summary>
public sealed class FakeAzureRbacClient : IAzureRbacClient
{
    public List<AzureRoleAssignment> Assignments { get; } = new();
    public List<AzureRoleDefinition> Definitions { get; } = new();
    public List<(string Caller, string Role, string Scope)> Created { get; } = new();
    public bool CreateResult { get; set; } = true;

    public Task<IReadOnlyList<AzureRoleAssignment>> GetRoleAssignmentsAsync(string callerId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AzureRoleAssignment>>(Assignments);

    public Task<IReadOnlyList<AzureRoleDefinition>> GetRoleDefinitionsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AzureRoleDefinition>>(Definitions);

    public Task<bool> CreateRoleAssignmentAsync(string callerId, string roleName, string scope, CancellationToken ct = default)
    {
        Created.Add((callerId, roleName, scope));
        return Task.FromResult(CreateResult);
    }
}

/// <summary>Returns a preset REST result, mirroring the Mock Invoke-RBACRestRequest in the Pester suite.</summary>
public sealed class FakeRestClient : IRestClient
{
    private readonly RestResult _result;
    public FakeRestClient(RestResult result) => _result = result;

    public Task<RestResult> SendAsync(string resourceUrl, string uri, string method = "GET", object? body = null, CancellationToken ct = default)
        => Task.FromResult(_result);

    public static FakeRestClient Ok(string content) =>
        new(new RestResult { Success = true, StatusCode = 200, IsAuthorizationError = false, Content = content, Message = string.Empty });

    public static FakeRestClient Forbidden() =>
        new(new RestResult { Success = false, StatusCode = 403, IsAuthorizationError = true, Content = null, Message = "Forbidden" });
}
