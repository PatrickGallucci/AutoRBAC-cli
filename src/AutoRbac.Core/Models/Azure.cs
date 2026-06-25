namespace AutoRbac.Core.Models;

/// <summary>A live or supplied Azure role assignment (role name + the scope it is held at).</summary>
public sealed record AzureRoleAssignment
{
    public required string RoleDefinitionName { get; init; }
    public required string Scope { get; init; }
}

/// <summary>
/// A built-in / custom Azure role definition, flattened to the action and not-action globs that
/// RoleActionMapper needs for least-privilege ranking.
/// </summary>
public sealed record AzureRoleDefinition
{
    public required string Name { get; init; }
    public IReadOnlyList<string> Actions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> NotActions { get; init; } = Array.Empty<string>();
}
