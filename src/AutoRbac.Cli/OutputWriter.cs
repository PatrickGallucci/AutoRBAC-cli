using System.Text.Json;
using System.Text.Json.Serialization;
using AutoRbac.Core.Models;

namespace AutoRbac.Cli;

public enum OutputFormat { Table, Json }

/// <summary>Renders AutoRBAC results as either indented JSON or a concise human-readable view.</summary>
public static class OutputWriter
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() },
    };

    private static void Dump(object value) => Console.WriteLine(JsonSerializer.Serialize(value, Json));

    public static void Providers(IReadOnlyList<ProviderInfo> providers, OutputFormat format)
    {
        if (format == OutputFormat.Json) { Dump(providers); return; }
        foreach (var p in providers)
        {
            Console.WriteLine($"{p.Name}");
            Console.WriteLine($"  Aliases           : {string.Join(", ", p.Aliases)}");
            Console.WriteLine($"  SupportsLiveProbe : {p.SupportsLiveProbe}");
        }
    }

    public static void Requirement(Requirement r, OutputFormat format)
    {
        if (format == OutputFormat.Json) { Dump(r); return; }
        Console.WriteLine($"Platform    : {r.Platform}");
        Console.WriteLine($"Command     : {r.Command}");
        Console.WriteLine($"Roles       : {string.Join(", ", r.Roles)}");
        Console.WriteLine($"Permissions : {string.Join(", ", r.Permissions)}");
        Console.WriteLine($"ScopeLevel  : {r.ScopeLevel}");
        Console.WriteLine($"IsKnown     : {r.IsKnown}");
        Console.WriteLine($"Source      : {r.Source}");
        if (!string.IsNullOrEmpty(r.Notes)) Console.WriteLine($"Notes       : {r.Notes}");
    }

    public static void AccessStates(IReadOnlyList<AccessState> states, OutputFormat format)
    {
        if (format == OutputFormat.Json) { Dump(states); return; }
        foreach (var s in states)
        {
            Console.WriteLine($"{s.Role,-40} HasAccess={Show(s.HasAccess)}  Scope={s.Scope}");
        }
    }

    public static void GrantScript(GrantScript g, OutputFormat format)
    {
        if (format == OutputFormat.Json) { Dump(g); return; }
        Console.WriteLine($"# Platform: {g.Platform}  Role: {g.Role}  Caller: {g.CallerId}  Scope: {g.Scope}");
        Console.WriteLine();
        Console.WriteLine("# ---- AddScript ----");
        Console.WriteLine(g.AddScript);
        Console.WriteLine("# ---- RemoveScript ----");
        Console.WriteLine(g.RemoveScript);
    }

    public static void ProbeResults(IReadOnlyList<ProbeResult> results, OutputFormat format)
    {
        if (format == OutputFormat.Json) { Dump(results); return; }
        foreach (var r in results)
        {
            Console.WriteLine($"Platform  : {r.Platform}");
            Console.WriteLine($"Command   : {r.Command}");
            Console.WriteLine($"Caller    : {r.CallerId}");
            Console.WriteLine($"Scope     : {r.Scope}");
            Console.WriteLine($"Role      : {r.Role ?? "(none)"}");
            Console.WriteLine($"HasAccess : {Show(r.HasAccess)}");
            Console.WriteLine($"Mode      : {r.Mode}");
            Console.WriteLine($"IsKnown   : {r.IsKnown}   Source: {r.Source}");
            if (r.Permissions.Count > 0) Console.WriteLine($"Permissions: {string.Join(", ", r.Permissions)}");
            if (!string.IsNullOrEmpty(r.Notes)) Console.WriteLine($"Notes     : {r.Notes}");
            Console.WriteLine(new string('-', 60));
        }
    }

    public static void AccessResults(IReadOnlyList<AccessResult> results, OutputFormat format)
    {
        if (format == OutputFormat.Json) { Dump(results); return; }
        foreach (var r in results)
        {
            Console.WriteLine($"Platform  : {r.Platform}");
            Console.WriteLine($"Command   : {r.Command}");
            Console.WriteLine($"Caller    : {r.CallerId}");
            Console.WriteLine($"Scope     : {r.Scope}");
            Console.WriteLine($"Role      : {r.Role}");
            Console.WriteLine($"HasAccess : {Show(r.HasAccess)}");
            Console.WriteLine($"Applied   : {r.Applied}");
            if (!string.IsNullOrEmpty(r.Notes)) Console.WriteLine($"Notes     : {r.Notes}");
            Console.WriteLine(new string('-', 60));
        }
    }

    private static string Show(bool? value) => value switch { true => "True", false => "False", _ => "(unknown)" };
}
