# AutoRBAC

[![CI](https://github.com/PatrickGallucci/AutoRBAC-cli/actions/workflows/ci.yml/badge.svg)](https://github.com/PatrickGallucci/AutoRBAC-cli/actions/workflows/ci.yml)
[![Docs](https://github.com/PatrickGallucci/AutoRBAC-cli/actions/workflows/docs.yml/badge.svg)](https://patrickgallucci.github.io/AutoRBAC-cli/)
[![NuGet](https://img.shields.io/nuget/v/AutoRbac.Cli.svg)](https://www.nuget.org/packages/AutoRbac.Cli)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**Live, least-privilege RBAC discovery and assignment across Azure, Microsoft Graph (Entra), Microsoft Fabric, and Microsoft Purview — as a cross-platform .NET CLI.**

📖 **Documentation: [patrickgallucci.github.io/AutoRBAC-cli](https://patrickgallucci.github.io/AutoRBAC-cli/)**

AutoRBAC answers *"What roles do I need to run this command, and do I already have them?"* — then acts on the answer. During provisioning it is often unknown which roles a script actually requires. Rather than granting blanket access, AutoRBAC supports a **Zero Trust**, **least-privilege**, **time-boxed** model: a framework identity that holds only *roleAssignments write* grants the caller *exactly* the roles an operation needs — and emits the snippet to revoke them afterward.

> This is the C# rewrite of the original `PSAutoRBAC` PowerShell module. The provider model, knowledge base, and behaviour are preserved; the surface is now a single self-contained CLI (`autorbac`) plus a reusable `AutoRbac.Core` library.

## Each platform is probed with its best primitive

A single "run the command, parse the `AuthorizationFailed`, extract the action" strategy **only works on Azure** — it's the one platform whose error names the missing action and scope. The other three don't surface that, so AutoRBAC uses a **per-platform provider model**, where each provider resolves with the right primitive:

| Platform | How the requirement is resolved | Live probe? |
|----------|---------------------------------|:-----------:|
| **Azure** (ARM) | Knowledge base + live role definitions; `--live-probe` parses an `AuthorizationFailed` action → role. | ✅ |
| **Microsoft Graph** | Knowledge base (the PowerShell-only `Find-MgGraphCommand` catalog has no SDK equivalent). Graph's error names nothing, so it is never parsed. | ❌ |
| **Microsoft Fabric** | Knowledge base; access checked via workspace `roleAssignments` REST. Roles are Admin > Member > Contributor > Viewer (a higher role satisfies a lower need). | ❌ |
| **Microsoft Purview** | Knowledge base; access checked via collection metadata policies (Collection Admin / Data Curator / Data Reader …). | ❌ |

`autorbac provider` lists the registered platforms and which support live probing.

## How it works

1. **Discover** — resolve the least-privilege role(s) a command needs (preflight, or live for Azure).
2. **Evaluate** — test whether the caller already holds them at the target scope (or an ancestor).
3. **Generate** — emit idempotent, platform-native grant *and* revoke snippets.
4. **Apply** — optionally grant missing roles (Azure), via `set-access --apply`.

## Build & install

Requires the **.NET 8 SDK**.

```bash
dotnet build                      # build the solution
dotnet test                       # run the xUnit suite
dotnet run --project src/AutoRbac.Cli -- provider   # run from source
```

Publish a self-contained single-file executable:

```bash
dotnet publish src/AutoRbac.Cli -c Release -r win-x64 --self-contained \
    -p:PublishSingleFile=true -o ./dist
# produces ./dist/autorbac(.exe)
```

Or install as a global tool from a local pack (the CLI project sets `ToolCommandName=autorbac`).

## Quick start

The flagship — *what does `New-AzResourceGroup` need, and do I have it?* Evaluate fully offline by supplying the caller's assignments:

```bash
autorbac probe --platform Azure --command New-AzResourceGroup \
    --caller-id patrick@contoso.com --subscription-id $SUB \
    --role-assignment "Reader@/subscriptions/$SUB"
```

Output (one block per required role): `Role=Contributor  HasAccess=False  Mode=Preflight`, plus idempotent grant/revoke snippets.

### Live access checks (Azure)

Drop `--role-assignment` and add `--live` to evaluate against the real directory using `DefaultAzureCredential` (Azure CLI login, managed identity, environment, …). In live mode `--caller-id` is the principal **object id**:

```bash
autorbac probe --platform Azure --command New-AzResourceGroup \
    --caller-id 11111111-2222-3333-4444-555555555555 --subscription-id $SUB --live
```

### Live probe (Azure only)

Derive the requirement from a real `AuthorizationFailed`. Run the operation yourself, then paste its error text:

```bash
autorbac probe --platform Azure --command New-AzStorageAccount \
    --caller-id dev@contoso.com --scope "/subscriptions/$SUB/resourceGroups/rg" \
    --live-probe --error-text "does not have authorization to perform action 'Microsoft.Storage/storageAccounts/write' over scope '/subscriptions/$SUB/resourceGroups/rg' (Code: AuthorizationFailed)"
# -> Role: Storage Account Contributor   (derived from the parsed action)
```

### Just the requirement

```bash
autorbac requirement --platform "Microsoft Graph" --command New-MgGroup
# Roles: Groups Administrator   Permissions: Group.ReadWrite.All
```

### Just the grant / revoke snippets

```bash
autorbac grant-script --platform Azure --caller-id dev@contoso.com \
    --role Reader --scope "/subscriptions/$SUB"
```

### End-to-end (discover → test → generate → apply)

```bash
# Report only (the safe default):
autorbac set-access --platform Azure --subscription-id $SUB \
    --caller-id dev@contoso.com --command New-AzResourceGroup --role-assignment "Reader@/subscriptions/$SUB"

# Grant missing Azure roles for real (requires --live + roleAssignments/write):
autorbac set-access --platform Azure --subscription-id $SUB \
    --caller-id <object-id> --command New-AzResourceGroup --live --apply
```

Add `--output json` to any command for machine-readable output.

## Commands

| Command | Purpose |
|---------|---------|
| `probe` | Flagship: resolve the requirement (preflight or live) and the caller's access in one call. |
| `requirement` | The minimum role(s) / permissions a command needs. |
| `test-access` | Whether a caller holds the required role(s) at a scope. |
| `grant-script` | Idempotent, platform-native grant + revoke snippets. |
| `set-access` | End-to-end orchestration; grants with `--apply`. |
| `provider` | List the registered platform providers. |

Run `autorbac <command> --help` for the full option list.

## Project layout

```text
src/AutoRbac.Core/    Reusable library: models, engine, providers, knowledge bases (embedded JSON)
src/AutoRbac.Cli/     System.CommandLine front end (the `autorbac` executable)
tests/AutoRbac.Tests/ xUnit suite (offline + live-path fakes), ported from the original Pester tests
```

## Extending

- **Knowledge base** — [`src/AutoRbac.Core/Data/CommandRoleMap.json`](src/AutoRbac.Core/Data/CommandRoleMap.json) maps `command → roles/actions/scope` per platform (pure data). [`RoleActionMap.json`](src/AutoRbac.Core/Data/RoleActionMap.json) maps Azure actions back to sensible roles for offline use. Both ship as embedded resources; pass `--map-path` to override the command map.
- **A new platform** — implement `IRbacProvider` (or subclass `ProviderBase`) and register it in `ProviderRegistry.CreateDefault()`. No engine changes required.

## License

[MIT](LICENSE)
