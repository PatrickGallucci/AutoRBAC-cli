using System.CommandLine;
using AutoRbac.Cli;
using AutoRbac.Core;
using AutoRbac.Core.Engine;
using AutoRbac.Core.Models;

var service = new RbacService();

// ----- shared option factories -------------------------------------------------------------------
Option<OutputFormat> OutputOption() => new("--output", "-o")
{
    Description = "Output format: table (default) or json.",
    DefaultValueFactory = _ => OutputFormat.Table,
};
Option<bool> LiveOption() => new("--live")
{
    Description = "Use a live context (DefaultAzureCredential) for Azure SDK / REST calls instead of offline evaluation.",
};
Option<string?> TenantOption() => new("--tenant-id") { Description = "Optional tenant id for the probe identity." };
Option<string?> MapPathOption() => new("--map-path") { Description = "Alternate knowledge-base JSON path (testing)." };
Option<string[]> RoleAssignmentOption() => new("--role-assignment")
{
    Description = "Pre-supplied assignment for offline evaluation: 'Role' or 'Role@Scope'. Repeatable.",
    AllowMultipleArgumentsPerToken = true,
};
Option<string?> WorkspaceIdOption() => new("--workspace-id") { Description = "Fabric workspace id (provider option)." };
Option<string?> CollectionOption() => new("--collection") { Description = "Purview collection name (provider option)." };
Option<string?> EndpointOption() => new("--endpoint") { Description = "Purview account data-plane endpoint (provider option)." };

ProbeOptions BuildOptions(ParseResult pr, Option<string?>? mapPath, Option<string[]>? roleAssignment,
    Option<string?>? workspace, Option<string?>? collection, Option<string?>? endpoint)
{
    var o = new ProbeOptions
    {
        MapPath = mapPath is null ? null : pr.GetValue(mapPath),
        WorkspaceId = workspace is null ? null : pr.GetValue(workspace),
        Collection = collection is null ? null : pr.GetValue(collection),
        Endpoint = endpoint is null ? null : pr.GetValue(endpoint),
    };
    if (roleAssignment is not null && pr.GetResult(roleAssignment) is not null)
    {
        o.RoleAssignment = (pr.GetValue(roleAssignment) ?? Array.Empty<string>())
            .Select(RoleAssignmentInput.Parse).ToList();
    }
    return o;
}

RbacContext BuildContext(ParseResult pr, Option<bool> live, Option<string?> tenant, string? defaultScope)
{
    var isLive = pr.GetValue(live);
    var tenantId = pr.GetValue(tenant);
    return isLive ? RbacContextFactory.Live(tenantId, defaultScope) : RbacContextFactory.Offline(tenantId);
}

void Warn(string message) => Console.Error.WriteLine("warning: " + message);

// ----- provider ----------------------------------------------------------------------------------
var providerCmd = new Command("provider", "List the registered platform providers.");
{
    var platform = new Option<string?>("--platform", "-p") { Description = "Optional platform name/alias to resolve a single provider." };
    var output = OutputOption();
    providerCmd.Options.Add(platform);
    providerCmd.Options.Add(output);
    providerCmd.SetAction(pr =>
    {
        OutputWriter.Providers(service.GetProviders(pr.GetValue(platform)), pr.GetValue(output));
        return 0;
    });
}

// ----- requirement -------------------------------------------------------------------------------
var requirementCmd = new Command("requirement", "Resolve the minimum role(s)/permissions a command requires.");
{
    var platform = new Option<string>("--platform", "-p") { Description = "Platform name or alias.", Required = true };
    var command = new Option<string>("--command", "-c") { Description = "Command / operation to evaluate.", Required = true };
    var mapPath = MapPathOption();
    var tenant = TenantOption();
    var live = LiveOption();
    var output = OutputOption();
    foreach (var o in new Option[] { platform, command, mapPath, tenant, live, output }) requirementCmd.Options.Add(o);
    requirementCmd.SetAction(async (pr, ct) =>
    {
        var opts = BuildOptions(pr, mapPath, null, null, null, null);
        using var ctx = BuildContext(pr, live, tenant, "/");
        var req = await service.GetRequirementAsync(pr.GetValue(platform)!, pr.GetValue(command)!, ctx, opts, ct);
        OutputWriter.Requirement(req, pr.GetValue(output));
        return 0;
    });
}

// ----- test-access -------------------------------------------------------------------------------
var testAccessCmd = new Command("test-access", "Test whether a caller holds the required role(s) at a scope.");
{
    var platform = new Option<string>("--platform", "-p") { Description = "Platform name or alias.", Required = true };
    var callerId = new Option<string>("--caller-id") { Description = "Identity being evaluated (UPN, sign-in name, or object id).", Required = true };
    var requiredRole = new Option<string[]>("--required-role") { Description = "Role(s) the caller is expected to hold. Repeatable.", AllowMultipleArgumentsPerToken = true, Required = true };
    var scope = new Option<string?>("--scope") { Description = "Scope to evaluate at (ARM scope, workspace id, Purview endpoint, or '/')." };
    var roleAssignment = RoleAssignmentOption();
    var workspace = WorkspaceIdOption();
    var collection = CollectionOption();
    var endpoint = EndpointOption();
    var tenant = TenantOption();
    var live = LiveOption();
    var output = OutputOption();
    foreach (var o in new Option[] { platform, callerId, requiredRole, scope, roleAssignment, workspace, collection, endpoint, tenant, live, output }) testAccessCmd.Options.Add(o);
    testAccessCmd.SetAction(async (pr, ct) =>
    {
        var opts = BuildOptions(pr, null, roleAssignment, workspace, collection, endpoint);
        var resolvedScope = pr.GetValue(scope) ?? "/";
        using var ctx = BuildContext(pr, live, tenant, resolvedScope);
        var states = await service.TestAccessAsync(pr.GetValue(platform)!, pr.GetValue(callerId)!, pr.GetValue(requiredRole)!, resolvedScope, ctx, opts, ct);
        OutputWriter.AccessStates(states, pr.GetValue(output));
        return 0;
    });
}

// ----- grant-script ------------------------------------------------------------------------------
var grantScriptCmd = new Command("grant-script", "Generate idempotent grant + revoke snippets for a role.");
{
    var platform = new Option<string>("--platform", "-p") { Description = "Platform name or alias.", Required = true };
    var callerId = new Option<string>("--caller-id") { Description = "Identity to grant or revoke.", Required = true };
    var role = new Option<string>("--role") { Description = "Role to assign.", Required = true };
    var scope = new Option<string>("--scope") { Description = "Scope the assignment applies to.", Required = true };
    var workspace = WorkspaceIdOption();
    var collection = CollectionOption();
    var endpoint = EndpointOption();
    var output = OutputOption();
    foreach (var o in new Option[] { platform, callerId, role, scope, workspace, collection, endpoint, output }) grantScriptCmd.Options.Add(o);
    grantScriptCmd.SetAction(pr =>
    {
        var opts = BuildOptions(pr, null, null, workspace, collection, endpoint);
        var g = service.NewGrantScript(pr.GetValue(platform)!, pr.GetValue(callerId)!, pr.GetValue(role)!, pr.GetValue(scope)!, opts);
        OutputWriter.GrantScript(g, pr.GetValue(output));
        return 0;
    });
}

// ----- shared scope options for probe / set-access -----------------------------------------------
(Option<string?> scope, Option<string?> sub, Option<string?> rg, Option<string?> mg, Option<string?> resId) ScopeOptions()
    => (new("--scope") { Description = "Explicit scope (overrides the Azure parts below)." },
        new("--subscription-id") { Description = "Azure subscription id (builds an ARM scope)." },
        new("--resource-group-name") { Description = "Azure resource group (builds an ARM scope)." },
        new("--management-group-id") { Description = "Azure management group (builds an ARM scope)." },
        new("--resource-id") { Description = "Explicit ARM resource id scope." });

ScopeInputs ReadScope(ParseResult pr, Option<string?> scope, Option<string?> sub, Option<string?> rg, Option<string?> mg, Option<string?> resId) => new()
{
    Scope = pr.GetValue(scope),
    SubscriptionId = pr.GetValue(sub),
    ResourceGroupName = pr.GetValue(rg),
    ManagementGroupId = pr.GetValue(mg),
    ResourceId = pr.GetValue(resId),
};

// ----- probe -------------------------------------------------------------------------------------
var probeCmd = new Command("probe", "Resolve the requirement (preflight or live) and the caller's access in one call.");
{
    var platform = new Option<string>("--platform", "-p") { Description = "Platform name or alias.", Required = true };
    var command = new Option<string>("--command", "-c") { Description = "Command / operation being probed.", Required = true };
    var callerId = new Option<string>("--caller-id") { Description = "Identity whose access is evaluated.", Required = true };
    var (scope, sub, rg, mg, resId) = ScopeOptions();
    var liveProbe = new Option<bool>("--live-probe") { Description = "Derive the Azure requirement from a live AuthorizationFailed (supply --error-text)." };
    var errorText = new Option<string?>("--error-text") { Description = "AuthorizationFailed error text to parse for --live-probe (Azure)." };
    var roleAssignment = RoleAssignmentOption();
    var mapPath = MapPathOption();
    var workspace = WorkspaceIdOption();
    var collection = CollectionOption();
    var endpoint = EndpointOption();
    var tenant = TenantOption();
    var live = LiveOption();
    var output = OutputOption();
    foreach (var o in new Option[] { platform, command, callerId, scope, sub, rg, mg, resId, liveProbe, errorText, roleAssignment, mapPath, workspace, collection, endpoint, tenant, live, output }) probeCmd.Options.Add(o);
    probeCmd.SetAction(async (pr, ct) =>
    {
        var opts = BuildOptions(pr, mapPath, roleAssignment, workspace, collection, endpoint);
        var scopeInputs = ReadScope(pr, scope, sub, rg, mg, resId);
        using var ctx = BuildContext(pr, live, tenant, ScopeResolver.Resolve(scopeInputs, allowTenantRoot: true));
        var results = await service.ProbeAsync(pr.GetValue(platform)!, pr.GetValue(command)!, pr.GetValue(callerId)!,
            scopeInputs, opts, ctx, pr.GetValue(liveProbe), pr.GetValue(errorText), Warn, ct);
        OutputWriter.ProbeResults(results, pr.GetValue(output));
        return 0;
    });
}

// ----- set-access --------------------------------------------------------------------------------
var setAccessCmd = new Command("set-access", "Discover, evaluate, and (with --apply) grant the least-privilege roles a command needs.");
{
    var platform = new Option<string>("--platform", "-p") { Description = "Platform name or alias.", Required = true };
    var command = new Option<string>("--command", "-c") { Description = "Command whose requirement is evaluated.", Required = true };
    var callerId = new Option<string>("--caller-id") { Description = "Identity to evaluate and grant.", Required = true };
    var (scope, sub, rg, mg, resId) = ScopeOptions();
    var apply = new Option<bool>("--apply") { Description = "Grant missing roles (Azure only). Without it, report only (the safe default)." };
    var liveProbe = new Option<bool>("--live-probe") { Description = "Derive the Azure requirement from a live AuthorizationFailed (supply --error-text)." };
    var errorText = new Option<string?>("--error-text") { Description = "AuthorizationFailed error text to parse for --live-probe (Azure)." };
    var roleAssignment = RoleAssignmentOption();
    var mapPath = MapPathOption();
    var workspace = WorkspaceIdOption();
    var collection = CollectionOption();
    var endpoint = EndpointOption();
    var tenant = TenantOption();
    var live = LiveOption();
    var output = OutputOption();
    foreach (var o in new Option[] { platform, command, callerId, scope, sub, rg, mg, resId, apply, liveProbe, errorText, roleAssignment, mapPath, workspace, collection, endpoint, tenant, live, output }) setAccessCmd.Options.Add(o);
    setAccessCmd.SetAction(async (pr, ct) =>
    {
        var opts = BuildOptions(pr, mapPath, roleAssignment, workspace, collection, endpoint);
        var scopeInputs = ReadScope(pr, scope, sub, rg, mg, resId);
        using var ctx = BuildContext(pr, live, tenant, ScopeResolver.Resolve(scopeInputs, allowTenantRoot: true));
        var results = await service.SetAccessAsync(pr.GetValue(platform)!, pr.GetValue(command)!, pr.GetValue(callerId)!,
            scopeInputs, opts, ctx, pr.GetValue(apply), pr.GetValue(liveProbe), pr.GetValue(errorText), Warn, ct);
        OutputWriter.AccessResults(results, pr.GetValue(output));
        return 0;
    });
}

var root = new RootCommand(
    "AutoRBAC - live, least-privilege RBAC discovery and assignment across Azure, Microsoft Graph (Entra), " +
    "Microsoft Fabric, and Microsoft Purview.")
{
    providerCmd, requirementCmd, testAccessCmd, grantScriptCmd, probeCmd, setAccessCmd,
};

try
{
    return await root.Parse(args).InvokeAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine("error: " + ex.Message);
    return 1;
}
