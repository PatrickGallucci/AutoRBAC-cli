using System.Text.RegularExpressions;

namespace AutoRbac.Core.Engine;

/// <summary>
/// Treats an Azure action pattern (possibly containing '*' wildcards) as matching a needed action,
/// case-insensitively. Direct port of the PowerShell engine's $matchAction helper.
/// </summary>
public static class Glob
{
    public static bool IsMatch(string? pattern, string needed)
    {
        if (string.IsNullOrEmpty(pattern)) return false;
        if (pattern == "*") return true;
        var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return Regex.IsMatch(needed, regex, RegexOptions.IgnoreCase);
    }
}
