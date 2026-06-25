using AutoRbac.Core.Engine;
using AutoRbac.Core.Models;
using AutoRbac.Core.Providers;

namespace AutoRbac.Tests;

public class ScopeResolverTests
{
    [Fact]
    public void Builds_a_subscription_scope() =>
        Assert.Equal("/subscriptions/SUB1", ScopeResolver.Resolve(new ScopeInputs { SubscriptionId = "SUB1" }));

    [Fact]
    public void Builds_a_management_group_scope() =>
        Assert.Equal("/providers/Microsoft.Management/managementGroups/mg1",
            ScopeResolver.Resolve(new ScopeInputs { ManagementGroupId = "mg1" }));

    [Fact]
    public void Builds_a_resource_group_scope() =>
        Assert.Equal("/subscriptions/SUB1/resourceGroups/rg1",
            ScopeResolver.Resolve(new ScopeInputs { SubscriptionId = "SUB1", ResourceGroupName = "rg1" }));

    [Fact]
    public void Returns_tenant_root_when_permitted_and_nothing_else_supplied() =>
        Assert.Equal("/", ScopeResolver.Resolve(new ScopeInputs(), allowTenantRoot: true));

    [Fact]
    public void Explicit_scope_wins_and_trailing_slash_is_trimmed() =>
        Assert.Equal("/subscriptions/SUB1", ScopeResolver.Resolve(new ScopeInputs { Scope = "/subscriptions/SUB1/" }));

    [Fact]
    public void Throws_when_no_scope_can_be_resolved() =>
        Assert.Throws<InvalidOperationException>(() => ScopeResolver.Resolve(new ScopeInputs()));
}

public class AzAuthorizationErrorParserTests
{
    [Fact]
    public void Extracts_action_and_scope_from_a_prose_message()
    {
        var msg = "The client 'app' with object id 'oid' does not have authorization to perform action " +
                  "'Microsoft.Storage/storageAccounts/write' over scope '/subscriptions/SUB1/resourceGroups/rg1' " +
                  "or the scope is invalid. (Code: AuthorizationFailed)";
        var p = AzAuthorizationErrorParser.Parse(msg);
        Assert.True(p.IsAuthorizationError);
        Assert.Contains("Microsoft.Storage/storageAccounts/write", p.Actions);
        Assert.Contains("/subscriptions/SUB1/resourceGroups/rg1", p.Scopes);
    }

    [Fact]
    public void Extracts_action_from_the_structured_ARM_error_form()
    {
        var json = "{\"error\":{\"code\":\"AuthorizationFailed\",\"message\":\"...\",\"action\":\"Microsoft.Compute/virtualMachines/write\",\"scope\":\"/subscriptions/SUB1\"}}";
        Assert.Contains("Microsoft.Compute/virtualMachines/write", AzAuthorizationErrorParser.Parse(json).Actions);
    }

    [Fact]
    public void Flags_a_non_authorization_message_as_not_an_auth_error() =>
        Assert.False(AzAuthorizationErrorParser.Parse("Some unrelated failure").IsAuthorizationError);
}

public class RoleActionMapperTests
{
    private static readonly KnowledgeBase Kb = KnowledgeBase.Default;

    [Fact]
    public void Maps_a_storage_write_action_via_the_curated_map()
    {
        var m = RoleActionMapper.Map(new[] { "Microsoft.Storage/storageAccounts/write" }, null, Kb.RoleActionEntries);
        Assert.Equal("CuratedMap", m.Source);
        Assert.Contains("Storage Account Contributor", m.Roles);
    }

    [Fact]
    public void Maps_a_read_action_to_Reader() =>
        Assert.Contains("Reader", RoleActionMapper.Map(new[] { "Microsoft.Resources/subscriptions/resourceGroups/read" }, null, Kb.RoleActionEntries).Roles);

    [Fact]
    public void Maps_roleAssignments_write_to_an_access_administration_role() =>
        Assert.Contains("User Access Administrator", RoleActionMapper.Map(new[] { "Microsoft.Authorization/roleAssignments/write" }, null, Kb.RoleActionEntries).Roles);

    [Fact]
    public void Ranks_specific_roles_above_wildcard_roles_via_live_definitions()
    {
        var defs = new List<AzureRoleDefinition>
        {
            new() { Name = "Owner", Actions = new[] { "*" } },
            new() { Name = "Widget Admin", Actions = new[] { "Microsoft.Widget/widgets/*" }, NotActions = new[] { "Microsoft.Widget/widgets/secret/read" } },
            new() { Name = "Widget Writer", Actions = new[] { "Microsoft.Widget/widgets/write" } },
        };
        var m = RoleActionMapper.Map(new[] { "Microsoft.Widget/widgets/write" }, defs, Array.Empty<RoleActionEntry>());
        Assert.Equal("RoleDefinition", m.Source);
        Assert.NotEqual("Owner", m.Roles[0]);
        Assert.Contains("Widget Writer", m.Roles);
    }

    [Fact]
    public void Honours_NotActions_when_matching()
    {
        var defs = new List<AzureRoleDefinition>
        {
            new() { Name = "Owner", Actions = new[] { "*" } },
            new() { Name = "Widget Admin", Actions = new[] { "Microsoft.Widget/widgets/*" }, NotActions = new[] { "Microsoft.Widget/widgets/secret/read" } },
        };
        var m = RoleActionMapper.Map(new[] { "Microsoft.Widget/widgets/secret/read" }, defs, Array.Empty<RoleActionEntry>());
        Assert.Contains("Owner", m.Roles);
    }

    [Fact]
    public void Falls_back_to_a_conservative_role_when_nothing_matches()
    {
        var m = RoleActionMapper.Map(new[] { "Microsoft.Nothing/matches/read" }, new List<AzureRoleDefinition>(), Array.Empty<RoleActionEntry>());
        Assert.Equal("Fallback", m.Source);
        Assert.Contains("Reader", m.Roles);
    }
}

public class ProviderRegistryTests
{
    [Fact]
    public void Throws_for_an_unregistered_platform() =>
        Assert.Throws<InvalidOperationException>(() => ProviderRegistry.CreateDefault().Resolve("DoesNotExist"));

    [Fact]
    public void Resolves_a_provider_by_alias() =>
        Assert.Equal("Microsoft Fabric", ProviderRegistry.CreateDefault().Resolve("Power BI").Name);
}
