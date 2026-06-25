using AutoRbac.Core;
using AutoRbac.Core.Engine;
using AutoRbac.Core.Models;

namespace AutoRbac.Tests;

public class LivePathTests
{
    private readonly RbacService _svc = new();

    // ---- Azure live access ----------------------------------------------------------------------
    [Fact]
    public async Task Evaluates_access_live_via_the_Azure_client()
    {
        var azure = new FakeAzureRbacClient();
        azure.Assignments.Add(new AzureRoleAssignment { RoleDefinitionName = "Reader", Scope = "/subscriptions/SUB1" });
        var ctx = new RbacContext { Azure = azure };

        var states = await _svc.TestAccessAsync("Azure", "oid", new[] { "Reader" }, "/subscriptions/SUB1", ctx, new ProbeOptions());
        Assert.True(states[0].HasAccess);
    }

    [Fact]
    public async Task Derives_the_requirement_from_a_live_failure_using_live_role_definitions()
    {
        // Use an action that no curated catch-all (*/read, */write, ...) matches, so the live
        // role-definition path engages and ranks the specific role above the wildcard 'Owner'.
        var azure = new FakeAzureRbacClient();
        var ctx = new RbacContext { Azure = azure };
        azure.Definitions.Add(new AzureRoleDefinition { Name = "Owner", Actions = new[] { "*" } });
        azure.Definitions.Add(new AzureRoleDefinition { Name = "Widget Operator", Actions = new[] { "Microsoft.Widget/widgets/frobnicate" } });

        var err = "does not have authorization to perform action 'Microsoft.Widget/widgets/frobnicate' over scope '/subscriptions/SUB1' (Code: AuthorizationFailed)";
        var results = await _svc.ProbeAsync("Azure", "Invoke-WidgetFrobnicate", "oid",
            new ScopeInputs { Scope = "/subscriptions/SUB1" }, new ProbeOptions { RoleAssignment = Array.Empty<RoleAssignmentInput>() }, ctx,
            liveProbe: true, errorText: err);

        // Specific role ranks first; the broad wildcard 'Owner' must not win the top slot.
        Assert.Equal("Widget Operator", results[0].Role);
        Assert.NotEqual("Owner", results[0].Role);
    }

    // ---- Set-access apply -----------------------------------------------------------------------
    [Fact]
    public async Task Grants_a_missing_Azure_role_and_marks_it_applied()
    {
        var azure = new FakeAzureRbacClient(); // no assignments => caller lacks the role
        var ctx = new RbacContext { Azure = azure };

        var r = (await _svc.SetAccessAsync("Azure", "New-AzResourceGroup", "11111111-1111-1111-1111-111111111111",
            new ScopeInputs { SubscriptionId = "SUB1" }, new ProbeOptions(), ctx, apply: true))[0];

        Assert.False(r.HasAccess);
        Assert.True(r.Applied);
        Assert.Single(azure.Created);
    }

    [Fact]
    public async Task Does_not_auto_apply_for_non_Azure_platforms()
    {
        var warnings = new List<string>();
        var opts = new ProbeOptions { RoleAssignment = new[] { RoleAssignmentInput.Parse("Viewer") } };
        var r = (await _svc.SetAccessAsync("Fabric", "New-FabricItem", "oid",
            new ScopeInputs { Scope = "ws1" }, opts, RbacContext.Offline(), apply: true, onWarning: warnings.Add))[0];

        Assert.False(r.Applied);
        Assert.Contains(warnings, w => w.Contains("not performed"));
    }

    // ---- Graph ----------------------------------------------------------------------------------
    [Fact]
    public async Task Graph_evaluates_access_against_supplied_scopes()
    {
        var opts = new ProbeOptions { RoleAssignment = new[] { RoleAssignmentInput.Parse("Group.ReadWrite.All") } };
        Assert.True((await _svc.TestAccessAsync("Microsoft Graph", "app", new[] { "Group.ReadWrite.All" }, "/", RbacContext.Offline(), opts))[0].HasAccess);
        Assert.False((await _svc.TestAccessAsync("Microsoft Graph", "app", new[] { "User.ReadWrite.All" }, "/", RbacContext.Offline(), opts))[0].HasAccess);
    }

    // ---- Fabric REST access check ---------------------------------------------------------------
    [Fact]
    public async Task Fabric_reads_workspace_role_assignments_and_applies_the_hierarchy()
    {
        const string json = """
            { "value": [ { "role": "Member", "principal": { "id": "oid", "userDetails": { "userPrincipalName": "u@b.com" } } } ] }
            """;
        var ctx = new RbacContext { Rest = FakeRestClient.Ok(json) };

        Assert.True((await _svc.TestAccessAsync("Fabric", "oid", new[] { "Contributor" }, "ws1", ctx, new ProbeOptions()))[0].HasAccess); // Member >= Contributor
        Assert.False((await _svc.TestAccessAsync("Fabric", "oid", new[] { "Admin" }, "ws1", ctx, new ProbeOptions()))[0].HasAccess);     // Member < Admin
    }

    [Fact]
    public async Task Fabric_treats_an_authorization_error_reading_assignments_as_no_access()
    {
        var ctx = new RbacContext { Rest = FakeRestClient.Forbidden() };
        Assert.False((await _svc.TestAccessAsync("Fabric", "oid", new[] { "Viewer" }, "ws1", ctx, new ProbeOptions()))[0].HasAccess);
    }

    // ---- Purview --------------------------------------------------------------------------------
    [Fact]
    public async Task Purview_resolves_a_metadata_policy_role_requirement() =>
        Assert.Contains("Data Curator", (await _svc.GetRequirementAsync("Purview", "Set-PurviewAsset", RbacContext.Offline(), new ProbeOptions())).Roles);

    [Fact]
    public async Task Purview_reads_collection_metadata_policies_to_evaluate_access()
    {
        const string json = """
            { "values": [ { "properties": { "attributeRules": [ { "id": "purviewmetadatapolicy:role:Data Reader", "dnfCondition": "principal caller-oid here" } ] } } ] }
            """;
        var ctx = new RbacContext { Rest = FakeRestClient.Ok(json) };
        var states = await _svc.TestAccessAsync("Purview", "caller-oid", new[] { "Data Reader" }, "https://acct.purview.azure.com", ctx, new ProbeOptions());
        Assert.True(states[0].HasAccess);
    }
}
