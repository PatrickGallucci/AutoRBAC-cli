using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace AutoRbac.Core.Engine;

/// <summary>A single command -> requirement entry in the knowledge base.</summary>
public sealed class CommandEntry
{
    public List<string> Roles { get; set; } = new();
    public List<string> Actions { get; set; } = new();
    public string? ScopeLevel { get; set; }
    public string? Notes { get; set; }
}

/// <summary>An ordered action-pattern -> role(s) entry in the reverse map.</summary>
public sealed class RoleActionEntry
{
    public string Pattern { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}

/// <summary>
/// Loads the command->requirement and role&lt;-action knowledge bases. Mirrors Get-RBACKnowledgeBase:
/// data ships as embedded JSON resources (the offline source) and is cached per source for the
/// process lifetime. An explicit file path overrides the embedded copy (chiefly for testing).
/// </summary>
public sealed class KnowledgeBase
{
    // platform (case-insensitive) -> command (case-insensitive) -> entry
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, CommandEntry>> _commandMap;
    private readonly IReadOnlyList<RoleActionEntry> _roleActionMap;

    private static readonly ConcurrentDictionary<string, KnowledgeBase> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private KnowledgeBase(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, CommandEntry>> commandMap,
        IReadOnlyList<RoleActionEntry> roleActionMap)
    {
        _commandMap = commandMap;
        _roleActionMap = roleActionMap;
    }

    /// <summary>The default knowledge base, built from embedded resources (cached).</summary>
    public static KnowledgeBase Default { get; } = LoadEmbedded();

    /// <summary>Load a command map from an explicit JSON file path (the RoleActionMap stays embedded).</summary>
    public static KnowledgeBase FromPath(string commandMapPath)
    {
        return Cache.GetOrAdd(Path.GetFullPath(commandMapPath), full =>
        {
            var commandMap = ParseCommandMap(File.ReadAllText(full));
            return new KnowledgeBase(commandMap, Default._roleActionMap);
        });
    }

    private static KnowledgeBase LoadEmbedded()
    {
        var commandMap = ParseCommandMap(ReadResource("CommandRoleMap.json"));
        var roleActionMap = ParseRoleActionMap(ReadResource("RoleActionMap.json"));
        return new KnowledgeBase(commandMap, roleActionMap);
    }

    private static string ReadResource(string fileName)
    {
        var asm = typeof(KnowledgeBase).Assembly;
        // Embedded resource names are "<RootNamespace>.Data.<fileName>".
        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("Data." + fileName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Embedded knowledge-base resource '{fileName}' not found.");
        using var stream = asm.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, CommandEntry>> ParseCommandMap(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var platforms = new Dictionary<string, IReadOnlyDictionary<string, CommandEntry>>(StringComparer.OrdinalIgnoreCase);

        foreach (var platform in doc.RootElement.EnumerateObject())
        {
            // Skip documentation keys (e.g. "_comment").
            if (platform.Name.StartsWith('_') || platform.Value.ValueKind != JsonValueKind.Object) continue;

            var commands = new Dictionary<string, CommandEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var command in platform.Value.EnumerateObject())
            {
                if (command.Name.StartsWith('_')) continue;
                var entry = command.Value.Deserialize<CommandEntry>(JsonOpts);
                if (entry is not null) commands[command.Name] = entry;
            }
            platforms[platform.Name] = commands;
        }
        return platforms;
    }

    private static IReadOnlyList<RoleActionEntry> ParseRoleActionMap(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("Entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
        {
            return entries.Deserialize<List<RoleActionEntry>>(JsonOpts) ?? new List<RoleActionEntry>();
        }
        return Array.Empty<RoleActionEntry>();
    }

    /// <summary>
    /// Resolves a command's entry for a platform, case-insensitively. Returns the platform '*Default'
    /// entry with isKnown=false when the command is unmapped.
    /// </summary>
    public (CommandEntry Entry, bool IsKnown) ResolveCommand(string platformKey, string command)
    {
        if (!_commandMap.TryGetValue(platformKey, out var commands))
        {
            throw new InvalidOperationException($"Knowledge base has no entries for platform '{platformKey}'.");
        }

        if (commands.TryGetValue(command, out var entry))
        {
            return (entry, true);
        }
        if (commands.TryGetValue("*Default", out var def))
        {
            return (def, false);
        }
        throw new InvalidOperationException($"Knowledge base for '{platformKey}' has no '*Default' entry.");
    }

    public IReadOnlyList<RoleActionEntry> RoleActionEntries => _roleActionMap;
}
