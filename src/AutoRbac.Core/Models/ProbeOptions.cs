namespace AutoRbac.Core.Models;

/// <summary>
/// Per-call provider options. Replaces the loosely-typed PowerShell -Options hashtable with an
/// explicit surface, plus the pre-supplied assignments used for fully offline evaluation.
/// </summary>
public sealed class ProbeOptions
{
    /// <summary>Alternate knowledge-base path (chiefly for testing). Overrides the embedded map.</summary>
    public string? MapPath { get; set; }

    /// <summary>Fabric workspace id used when emitting a grant snippet.</summary>
    public string? WorkspaceId { get; set; }

    /// <summary>Purview collection name used when emitting a grant snippet.</summary>
    public string? Collection { get; set; }

    /// <summary>Purview account data-plane endpoint used when emitting a grant snippet.</summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Pre-fetched assignments/roles for offline access evaluation. When set, providers never make
    /// live calls. Null means "evaluate live" (Azure SDK / REST).
    /// </summary>
    public IReadOnlyList<RoleAssignmentInput>? RoleAssignment { get; set; }

    /// <summary>True when <see cref="RoleAssignment"/> was explicitly supplied (even if empty).</summary>
    public bool HasRoleAssignment => RoleAssignment is not null;

    public ProbeOptions Clone() => new()
    {
        MapPath = MapPath,
        WorkspaceId = WorkspaceId,
        Collection = Collection,
        Endpoint = Endpoint,
        RoleAssignment = RoleAssignment,
    };
}
