using AutoRbac.Core;
using AutoRbac.Core.Engine;
using AutoRbac.Core.Models;

namespace AutoRbac.Tests;

public class ServiceTests
{
    private readonly RbacService _svc = new();

    // A caller who holds only Reader at the subscription.
    private static ProbeOptions ReaderOnly() => new()
    {
        RoleAssignment = new[] { new RoleAssignmentInput { Role = "Reader", Scope = "/subscriptions/SUB1" } },
    };

    private static ProbeOptions Supplied(params string[] roles) => new()
    {
        RoleAssignment = roles.Select(RoleAssignmentInput.Parse).ToList(),
    };

    // ---- providers ------------------------------------------------------------------------------
    [Fact]
    public void Registers_the_four_platform_providers()
    {
        var names = _svc.GetProviders().Select(p => p.Name).ToList();
        foreach (var p in new[] { "Azure", "Microsoft Graph", "Microsoft Fabric", "Microsoft Purview" })
            Assert.Contains(p, names);
    }

    [Fact]
    public void Reports_Azure_as_the_only_live_probe_capable_provider() =>
        Assert.Equal(new[] { "Azure" }, _svc.GetProviders().Where(p => p.SupportsLiveProbe).Select(p => p.Name));

    // ---- requirement ----------------------------------------------------------------------------
    [Fact]
    public async Task Returns_the_Reader_requirement_for_Connect_AzAccount()
    {
        var r = await _svc.GetRequirementAsync("Azure", "Connect-AzAccount", RbacContext.Offline(), new ProbeOptions());
        Assert.Contains("Reader", r.Roles);
        Assert.True(r.IsKnown);
        Assert.Equal("Subscription", r.ScopeLevel);
    }

    [Fact]
    public async Task Returns_Contributor_for_New_AzResourceGroup() =>
        Assert.Contains("Contributor", (await _svc.GetRequirementAsync("Azure", "New-AzResourceGroup", RbacContext.Offline(), new ProbeOptions())).Roles);

    [Fact]
    public async Task Falls_back_to_the_platform_default_for_an_unknown_command()
    {
        var r = await _svc.GetRequirementAsync("Azure", "Invoke-SomethingUnknown", RbacContext.Offline(), new ProbeOptions());
        Assert.False(r.IsKnown);
        Assert.Contains("Contributor", r.Roles);
    }

    [Fact]
    public async Task Matches_a_platform_alias_case_insensitively() =>
        Assert.Equal("Azure", (await _svc.GetRequirementAsync("azure powershell", "Connect-AzAccount", RbacContext.Offline(), new ProbeOptions())).Platform);

    [Fact]
    public async Task Resolves_a_Graph_directory_role_requirement() =>
        Assert.Contains("Groups Administrator", (await _svc.GetRequirementAsync("Microsoft Graph", "New-MgGroup", RbacContext.Offline(), new ProbeOptions())).Roles);

    [Fact]
    public async Task Throws_for_an_unknown_platform() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => _svc.GetRequirementAsync("Nope", "x", RbacContext.Offline(), new ProbeOptions()));

    // ---- test-access (Azure, offline) -----------------------------------------------------------
    [Fact]
    public async Task Reports_access_when_the_caller_holds_the_role_at_scope() =>
        Assert.True((await TestAzure("Reader", "/subscriptions/SUB1", ReaderOnly()))[0].HasAccess);

    [Fact]
    public async Task Honours_scope_inheritance() =>
        Assert.True((await TestAzure("Reader", "/subscriptions/SUB1/resourceGroups/rg1", ReaderOnly()))[0].HasAccess);

    [Fact]
    public async Task Reports_no_access_when_the_caller_lacks_the_role() =>
        Assert.False((await TestAzure("Owner", "/subscriptions/SUB1", ReaderOnly()))[0].HasAccess);

    [Fact]
    public async Task Returns_one_result_per_required_role()
    {
        var states = await _svc.TestAccessAsync("Azure", "a@b.com", new[] { "Reader", "Owner" }, "/subscriptions/SUB1", RbacContext.Offline(), ReaderOnly());
        Assert.Equal(2, states.Count);
    }

    private Task<IReadOnlyList<AccessState>> TestAzure(string role, string scope, ProbeOptions opts) =>
        _svc.TestAccessAsync("Azure", "a@b.com", new[] { role }, scope, RbacContext.Offline(), opts);

    // ---- test-access (Fabric hierarchy) ---------------------------------------------------------
    [Fact]
    public async Task Treats_a_higher_workspace_role_as_satisfying_a_lower_requirement() =>
        Assert.True((await _svc.TestAccessAsync("Fabric", "oid", new[] { "Contributor" }, "ws1", RbacContext.Offline(), Supplied("Admin")))[0].HasAccess);

    [Fact]
    public async Task Reports_no_access_when_the_held_role_is_lower_than_required() =>
        Assert.False((await _svc.TestAccessAsync("Fabric", "oid", new[] { "Member" }, "ws1", RbacContext.Offline(), Supplied("Viewer")))[0].HasAccess);

    // ---- grant-script ---------------------------------------------------------------------------
    [Fact]
    public void Azure_grant_script_is_idempotent_and_embeds_caller_role_scope()
    {
        var g = _svc.NewGrantScript("Azure", "a@b.com", "Reader", "/subscriptions/SUB1", new ProbeOptions());
        Assert.Contains("New-AzRoleAssignment", g.AddScript);
        Assert.Contains("Get-AzRoleAssignment", g.AddScript);
        Assert.Contains("Remove-AzRoleAssignment", g.RemoveScript);
        Assert.Contains("a@b.com", g.AddScript);
        Assert.Contains("Reader", g.AddScript);
        Assert.Contains("/subscriptions/SUB1", g.AddScript);
    }

    [Fact]
    public void Fabric_grant_script_emits_a_workspace_role_assignment_REST_snippet()
    {
        var g = _svc.NewGrantScript("Fabric", "oid", "Member", "ws1", new ProbeOptions { WorkspaceId = "ws1" });
        Assert.Contains("api.fabric.microsoft.com", g.AddScript);
        Assert.Contains("roleAssignments", g.AddScript);
    }

    [Fact]
    public void Graph_grant_script_emits_a_directory_role_assignment_snippet() =>
        Assert.Contains("RoleManagementDirectoryRoleAssignment", _svc.NewGrantScript("Microsoft Graph", "a@b.com", "Groups Administrator", "/", new ProbeOptions()).AddScript);

    [Fact]
    public void Purview_grant_script_emits_metadata_policy_guidance()
    {
        var g = _svc.NewGrantScript("Purview", "oid", "Data Curator", "https://acct.purview.azure.com", new ProbeOptions { Collection = "col1" });
        Assert.Contains("metadataPolicies", g.AddScript);
        Assert.Contains("metadata-policy", g.RemoveScript);
    }

    // ---- probe (preflight, offline) -------------------------------------------------------------
    [Fact]
    public async Task Probe_resolves_the_requirement_and_reports_the_access_verdict()
    {
        var r = (await _svc.ProbeAsync("Azure", "New-AzResourceGroup", "a@b.com",
            new ScopeInputs { SubscriptionId = "SUB1" }, ReaderOnly(), RbacContext.Offline()))[0];
        Assert.Equal("Contributor", r.Role);
        Assert.False(r.HasAccess);
        Assert.Equal(ProbeMode.Preflight, r.Mode);
        Assert.Equal("/subscriptions/SUB1", r.Scope);
    }

    [Fact]
    public async Task Probe_builds_a_resource_group_scope_from_subscription_and_group()
    {
        var r = (await _svc.ProbeAsync("Azure", "New-AzResourceGroup", "a@b.com",
            new ScopeInputs { SubscriptionId = "SUB1", ResourceGroupName = "rg1" }, ReaderOnly(), RbacContext.Offline()))[0];
        Assert.Equal("/subscriptions/SUB1/resourceGroups/rg1", r.Scope);
    }

    [Fact]
    public async Task Probe_reports_access_true_when_the_caller_already_holds_the_role() =>
        Assert.True((await _svc.ProbeAsync("Azure", "Connect-AzAccount", "a@b.com",
            new ScopeInputs { SubscriptionId = "SUB1" }, ReaderOnly(), RbacContext.Offline()))[0].HasAccess);

    [Fact]
    public async Task Probe_attaches_grant_and_revoke_snippets()
    {
        var r = (await _svc.ProbeAsync("Azure", "New-AzResourceGroup", "a@b.com",
            new ScopeInputs { SubscriptionId = "SUB1" }, ReaderOnly(), RbacContext.Offline()))[0];
        Assert.Contains("New-AzRoleAssignment", r.AddScript);
        Assert.Contains("Remove-AzRoleAssignment", r.RemoveScript);
    }

    [Fact]
    public async Task Probe_warns_and_falls_back_to_preflight_for_live_probe_on_a_non_Azure_platform()
    {
        var warnings = new List<string>();
        var r = (await _svc.ProbeAsync("Microsoft Fabric", "New-FabricItem", "oid",
            new ScopeInputs { Scope = "ws1" }, Supplied("Viewer"), RbacContext.Offline(),
            liveProbe: true, onWarning: warnings.Add))[0];
        Assert.Equal(ProbeMode.Preflight, r.Mode);
        Assert.Contains(warnings, w => w.Contains("does not support live probing"));
    }

    // ---- set-access (offline, report-only) ------------------------------------------------------
    [Fact]
    public async Task Set_access_reports_state_without_applying_by_default()
    {
        var r = (await _svc.SetAccessAsync("Azure", "New-AzResourceGroup", "a@b.com",
            new ScopeInputs { SubscriptionId = "SUB1" }, ReaderOnly(), RbacContext.Offline()))[0];
        Assert.Equal("Contributor", r.Role);
        Assert.False(r.HasAccess);
        Assert.False(r.Applied);
    }

    // ---- live-probe (derive from AuthorizationFailed) -------------------------------------------
    [Fact]
    public async Task Derives_the_requirement_from_a_live_AuthorizationFailed()
    {
        var err = "does not have authorization to perform action 'Microsoft.Storage/storageAccounts/write' " +
                  "over scope '/subscriptions/SUB1/resourceGroups/rg' (Code: AuthorizationFailed)";
        var results = await _svc.ProbeAsync("Azure", "New-AzStorageAccount", "a@b.com",
            new ScopeInputs { Scope = "/subscriptions/SUB1/resourceGroups/rg" }, Supplied(), RbacContext.Offline(),
            liveProbe: true, errorText: err);
        Assert.All(results, r => Assert.Equal(ProbeMode.LiveProbe, r.Mode));
        Assert.Contains(results, r => r.Role == "Storage Account Contributor");
    }
}
