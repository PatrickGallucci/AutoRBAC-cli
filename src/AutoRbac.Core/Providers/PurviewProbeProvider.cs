using System.Text.Json;
using System.Text.RegularExpressions;
using AutoRbac.Core.Models;

namespace AutoRbac.Core.Providers;

/// <summary>
/// The Microsoft Purview RBAC probe provider. Purview governs data-plane access through metadata
/// policies (collection-scoped roles such as Collection Admin, Data Curator, Data Reader), not the
/// Azure action model, and its 403s carry no role detail. Requirements resolve from the knowledge
/// base; access is evaluated from supplied roles (offline) or by reading the account's metadata
/// policies via REST (endpoint supplied as the scope).
/// </summary>
public sealed class PurviewProbeProvider : ProviderBase
{
    private const string ResourceUrl = "https://purview.azure.net";
    private static readonly Regex RoleInRuleId = new(":role:([^:]+)", RegexOptions.Compiled);

    public override string Name => "Microsoft Purview";
    public override IReadOnlyList<string> Aliases => new[] { "Purview", "Atlas" };
    public override bool SupportsLiveProbe => false;
    protected override string DataKey => "Microsoft Purview";

    public override async Task<IReadOnlyList<AccessState>> TestAccessAsync(
        string callerId, IReadOnlyList<string> requiredRoles, string scope, RbacContext context, ProbeOptions options, CancellationToken ct = default)
    {
        var heldRoles = new List<string>();
        var evaluated = true;

        if (options.HasRoleAssignment)
        {
            heldRoles.AddRange(SuppliedRoles(options));
        }
        else if (!string.IsNullOrEmpty(scope) && Regex.IsMatch(scope, "^https?://") && context.Rest is not null)
        {
            var endpoint = scope.TrimEnd('/');
            var uri = $"{endpoint}/policystore/metadataPolicies?api-version=2021-07-01";
            var res = await context.Rest.SendAsync(ResourceUrl, uri, "GET", null, ct).ConfigureAwait(false);
            if (res.Success)
            {
                heldRoles.AddRange(ParseHeldRoles(res.Content, callerId));
            }
            else if (res.IsAuthorizationError)
            {
                heldRoles.Clear();
            }
            else
            {
                evaluated = false;
            }
        }
        else
        {
            evaluated = false;
        }

        var states = requiredRoles.Select(role => new AccessState
        {
            Platform = Name,
            CallerId = callerId,
            Role = role,
            Scope = scope,
            HasAccess = evaluated ? heldRoles.Contains(role, StringComparer.OrdinalIgnoreCase) : (bool?)null,
        }).ToList();

        return states;
    }

    private static IEnumerable<string> ParseHeldRoles(string? content, string callerId)
    {
        if (string.IsNullOrEmpty(content)) yield break;
        using var doc = JsonDocument.Parse(content);
        if (!doc.RootElement.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array) yield break;

        foreach (var policy in values.EnumerateArray())
        {
            if (!policy.TryGetProperty("properties", out var props)) continue;
            if (!props.TryGetProperty("attributeRules", out var rules) || rules.ValueKind != JsonValueKind.Array) continue;

            foreach (var rule in rules.EnumerateArray())
            {
                var refsCaller = rule.GetRawText().Contains(callerId, StringComparison.Ordinal);
                var id = rule.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                if (refsCaller && id is not null)
                {
                    var m = RoleInRuleId.Match(id);
                    if (m.Success) yield return m.Groups[1].Value;
                }
            }
        }
    }

    public override async Task<Requirement> ProbeLiveAsync(string command, string? errorText, string scope, RbacContext context, CancellationToken ct = default)
    {
        var req = await ResolveRequirementAsync(command, context, new ProbeOptions(), ct).ConfigureAwait(false);
        return req with
        {
            Notes = "Purview 403s do not name the required role; requirement resolved from the knowledge base, not a live failure.",
        };
    }

    public override GrantScript NewGrantScript(string callerId, string role, string scope, ProbeOptions options)
    {
        var endpoint = options.Endpoint ?? scope;
        var collection = options.Collection ?? "<rootCollectionName>";
        var add = AddTemplate.Replace("__ENDPOINT__", endpoint).Replace("__COLLECTION__", collection).Replace("__ROLE__", role).Replace("__CALLER__", callerId);
        var remove = RemoveTemplate.Replace("__COLLECTION__", collection).Replace("__ROLE__", role).Replace("__CALLER__", callerId);
        return new GrantScript { Platform = Name, CallerId = callerId, Role = role, Scope = scope.TrimEnd('/'), AddScript = add, RemoveScript = remove };
    }

    private const string AddTemplate = @"# AutoRBAC: grant Purview data-plane role '__ROLE__' to '__CALLER__' on collection '__COLLECTION__'.
# Purview roles are granted by editing the collection's METADATA POLICY (policystore API),
# not by a simple role-assignment call. Run as a Collection Admin.
# 1) GET the metadata policy for the collection:
#    GET __ENDPOINT__/policystore/metadataPolicies?api-version=2021-07-01&collectionName=__COLLECTION__
# 2) In the returned policy, find the attributeRule whose id ends in ':role:__ROLE__'
#    (e.g. purviewmetadatarole_builtin_data-curator) and add the principal id
#    '__CALLER__' to its 'fromRule' attributeValueIncludes entries.
# 3) PUT the modified policy back:
#    PUT __ENDPOINT__/policystore/metadataPolicies/{policyId}?api-version=2021-07-01
# See: https://learn.microsoft.com/purview/tutorial-metadata-policy-roles-apis
Write-Warning ""Purview role grants require a metadata-policy edit; review and apply the steps above for '__ROLE__' / '__CALLER__'.""
";

    private const string RemoveTemplate = @"# AutoRBAC: revoke Purview data-plane role '__ROLE__' from '__CALLER__' on collection '__COLLECTION__'.
# Reverse of the grant: GET the metadata policy, remove '__CALLER__' from the
# attributeRule for ':role:__ROLE__', then PUT the policy back. Run as a Collection Admin.
Write-Warning ""Purview role revokes require a metadata-policy edit; remove '__CALLER__' from the '__ROLE__' rule and PUT the policy back.""
";
}
