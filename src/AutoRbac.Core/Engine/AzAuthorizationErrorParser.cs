using System.Text.RegularExpressions;
using AutoRbac.Core.Models;

namespace AutoRbac.Core.Engine;

/// <summary>
/// Parses an Azure Resource Manager AuthorizationFailed error into actions + scope. Direct port of
/// ConvertFrom-AzAuthorizationError. Azure (ARM) is the only supported platform whose authorization
/// error reliably names the missing action and scope, e.g.:
///
///   "The client 'x' ... does not have authorization to perform action
///    'Microsoft.Storage/storageAccounts/write' over scope '/subscriptions/.../resourceGroups/rg' ..."
///
/// or the structured ARM-error JSON variant carrying explicit "action"/"scope" fields.
/// </summary>
public static class AzAuthorizationErrorParser
{
    private static readonly Regex ProseAction = new("perform action '([^']+)'", RegexOptions.Compiled);
    private static readonly Regex JsonAction = new("\"(?:action|dataAction)\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex ProseScope = new("over scope '([^']+)'", RegexOptions.Compiled);
    private static readonly Regex JsonScope = new("\"scope\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex AuthSignal = new(
        "AuthorizationFailed|does not have authorization to perform action|LinkedAuthorizationFailed|RoleAssignmentDoesNotExist",
        RegexOptions.Compiled);

    public static ParsedError Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new ParsedError { Actions = Array.Empty<string>(), Scopes = Array.Empty<string>(), IsAuthorizationError = false, Message = string.Empty };
        }

        var isAuth = AuthSignal.IsMatch(text);

        var actions = new List<string>();
        foreach (Match m in ProseAction.Matches(text)) actions.Add(m.Groups[1].Value);
        foreach (Match m in JsonAction.Matches(text)) actions.Add(m.Groups[1].Value);

        var scopes = new List<string>();
        foreach (Match m in ProseScope.Matches(text)) scopes.Add(m.Groups[1].Value);
        foreach (Match m in JsonScope.Matches(text)) scopes.Add(m.Groups[1].Value);

        return new ParsedError
        {
            Actions = Distinct(actions),
            Scopes = Distinct(scopes),
            IsAuthorizationError = isAuth,
            Message = text.Trim(),
        };
    }

    private static IReadOnlyList<string> Distinct(IEnumerable<string> values) =>
        values.Where(v => !string.IsNullOrEmpty(v)).Distinct(StringComparer.Ordinal).ToList();
}
