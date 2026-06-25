---
_layout: landing
---

# AutoRBAC

**Live, least-privilege RBAC discovery and assignment across Azure, Microsoft Graph (Entra), Microsoft Fabric, and Microsoft Purview — as a cross-platform .NET CLI.**

AutoRBAC answers *"What roles do I need to run this command, and do I already have them?"* — then acts on the answer. Rather than granting blanket access during provisioning, it supports a **Zero Trust**, **least-privilege**, **time-boxed** model: a framework identity holding only *roleAssignments write* grants the caller *exactly* the roles an operation needs — and emits the snippet to revoke them afterward.

<div class="hero-buttons">

[Get Started](articles/getting-started.md) &middot;
[Installation](articles/installation.md) &middot;
[Commands](articles/commands.md) &middot;
[API Reference](api/index.md)

</div>

## Each platform is probed with its best primitive

| Platform | How the requirement is resolved | Live probe? |
|----------|---------------------------------|:-----------:|
| **Azure** (ARM) | Knowledge base + live role definitions; `--live-probe` parses an `AuthorizationFailed` action → role. | ✅ |
| **Microsoft Graph** | Knowledge base (Graph's error names nothing, so it is never parsed). | ❌ |
| **Microsoft Fabric** | Knowledge base; access via workspace `roleAssignments` REST. Admin > Member > Contributor > Viewer. | ❌ |
| **Microsoft Purview** | Knowledge base; access via collection metadata policies. | ❌ |

## How it works

1. **Discover** — resolve the least-privilege role(s) a command needs (preflight, or live for Azure).
2. **Evaluate** — test whether the caller already holds them at the target scope (or an ancestor).
3. **Generate** — emit idempotent, platform-native grant *and* revoke snippets.
4. **Apply** — optionally grant missing roles (Azure), via `set-access --apply`.

```bash
autorbac probe --platform Azure --command New-AzResourceGroup \
    --caller-id dev@contoso.com --subscription-id $SUB \
    --role-assignment "Reader@/subscriptions/$SUB"
```

> This is the C# rewrite of the original `PSAutoRBAC` PowerShell module. The provider model, knowledge base, and behaviour are preserved; the surface is now a single self-contained CLI (`autorbac`) plus a reusable `AutoRbac.Core` library.
