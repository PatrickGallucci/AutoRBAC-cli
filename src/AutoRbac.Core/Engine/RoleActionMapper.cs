using System.Text.RegularExpressions;
using AutoRbac.Core.Models;

namespace AutoRbac.Core.Engine;

/// <summary>
/// Maps one or more Azure control-plane actions to candidate built-in roles. Direct port of
/// ConvertTo-RBACRole, in priority order:
///   1. The curated RoleActionMap (action glob -> sensible least-privilege role).
///   2. Live role definitions (authoritative long-tail), ranked with a heavy wildcard penalty so
///      broad roles (Owner/Contributor) rank below specific ones.
///   3. A conservative fallback (Contributor for write/delete/action, Reader otherwise).
/// </summary>
public static class RoleActionMapper
{
    private static readonly Regex WriteLike = new("/(write|delete|action)$|/\\*$", RegexOptions.Compiled);

    public static RoleMatch Map(
        IReadOnlyList<string> action,
        IReadOnlyList<AzureRoleDefinition>? liveDefinitions,
        IReadOnlyList<RoleActionEntry> curatedMap,
        int maxCandidates = 3)
    {
        var actions = action.Where(a => !string.IsNullOrEmpty(a)).Distinct(StringComparer.Ordinal).ToList();
        if (actions.Count == 0)
        {
            return new RoleMatch { Roles = Array.Empty<string>(), Source = "None", Actions = Array.Empty<string>() };
        }

        // 1) Curated offline RoleActionMap (action glob -> sensible role(s)).
        var offline = new List<string>();
        foreach (var a in actions)
        {
            foreach (var entry in curatedMap)
            {
                if (Glob.IsMatch(entry.Pattern, a)) offline.AddRange(entry.Roles);
            }
        }
        var curated = offline.Where(r => !string.IsNullOrEmpty(r)).Distinct(StringComparer.OrdinalIgnoreCase).Take(maxCandidates).ToList();
        if (curated.Count > 0)
        {
            return new RoleMatch { Roles = curated, Source = "CuratedMap", Actions = actions };
        }

        // 2) Live role definitions (authoritative long-tail, least-privilege ranked).
        if (liveDefinitions is { Count: > 0 })
        {
            var hits = new List<(string Name, int Weight)>();
            foreach (var def in liveDefinitions)
            {
                var defActions = def.Actions.Where(a => !string.IsNullOrEmpty(a)).Distinct(StringComparer.Ordinal).ToList();
                var defNot = def.NotActions.Where(a => !string.IsNullOrEmpty(a)).ToList();

                var allCovered = actions.All(a =>
                    defActions.Any(p => Glob.IsMatch(p, a)) &&
                    !defNot.Any(p => Glob.IsMatch(p, a)));

                if (allCovered)
                {
                    var wildcards = defActions.Count(p => p.Contains('*'));
                    hits.Add((def.Name, wildcards * 1000 + defActions.Count));
                }
            }

            var roles = hits.OrderBy(h => h.Weight).Select(h => h.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase).Take(maxCandidates).ToList();
            if (roles.Count > 0)
            {
                return new RoleMatch { Roles = roles, Source = "RoleDefinition", Actions = actions };
            }
        }

        // 3) Conservative fallback.
        var needsWrite = actions.Any(a => a == "*" || WriteLike.IsMatch(a));
        var fallback = needsWrite ? "Contributor" : "Reader";
        return new RoleMatch { Roles = new[] { fallback }, Source = "Fallback", Actions = actions };
    }
}
