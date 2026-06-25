# Getting Started

AutoRBAC is a cross-platform .NET 8 CLI (`autorbac`) that resolves the least-privilege RBAC a command needs, checks whether a caller already holds it, and emits idempotent grant/revoke snippets.

## Prerequisites

- **.NET 8 SDK** (to build/run from source) or the **.NET 8 runtime** (to run a published binary or installed tool).
- For **live** Azure checks/grants: an identity reachable by [`DefaultAzureCredential`](https://learn.microsoft.com/dotnet/api/azure.identity.defaultazurecredential) (Azure CLI sign-in, managed identity, environment variables, …).

## 60-second tour

```bash
# 1. List the registered platform providers.
autorbac provider

# 2. What does this command require?
autorbac requirement --platform Azure --command New-AzResourceGroup

# 3. Does a caller already have it? (fully offline, using supplied assignments)
autorbac probe --platform Azure --command New-AzResourceGroup \
    --caller-id dev@contoso.com --subscription-id $SUB \
    --role-assignment "Reader@/subscriptions/$SUB"

# 4. Get the grant/revoke snippets for a role.
autorbac grant-script --platform Azure --caller-id dev@contoso.com \
    --role Contributor --scope "/subscriptions/$SUB"
```

Every command accepts `--output json` for machine-readable output and `--help` for the full option list.

## Next steps

- [Installation](installation.md) — build from source, publish a binary, or install as a .NET tool.
- [Usage](usage.md) — offline vs. live, the live probe, and end-to-end grants.
- [Commands](commands.md) — the full subcommand reference.
- [Configuration](configuration.md) — knowledge base, credentials, and provider options.
