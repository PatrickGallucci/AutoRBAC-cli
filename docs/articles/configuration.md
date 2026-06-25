# Configuration

AutoRBAC has no config file — behaviour is driven by command options, the embedded knowledge base, and ambient Azure credentials.

## Credentials (live mode)

`--live` (and `set-access --apply`) authenticate with [`DefaultAzureCredential`](https://learn.microsoft.com/dotnet/api/azure.identity.defaultazurecredential), which tries, in order: environment variables, workload identity, managed identity, the Azure CLI (`az login`), Azure PowerShell, and more. Use `--tenant-id` to pin a tenant.

The Fabric / Purview REST checks acquire a bearer token for the target resource from the same credential.

## Knowledge base

Two data files drive offline resolution, shipped as **embedded resources** in `AutoRbac.Core`:

| File | Purpose |
|------|---------|
| `Data/CommandRoleMap.json` | `command → roles / actions / scope level` per platform. |
| `Data/RoleActionMap.json` | Ordered Azure `action glob → role(s)` reverse map for offline action-to-role mapping. |

Override the command map at runtime with `--map-path <file.json>` (same JSON shape). Unknown commands resolve to the platform `*Default` entry and report `IsKnown = false`.

## Provider options

| Option | Applies to | Meaning |
|--------|-----------|---------|
| `--workspace-id` | Fabric | Workspace id used when emitting a grant snippet. |
| `--collection` | Purview | Collection name used in the grant guidance. |
| `--endpoint` | Purview | Account data-plane endpoint (`https://{account}.purview.azure.com`). |
| `--scope` | all | ARM scope (Azure), workspace id (Fabric), account endpoint (Purview), or `/`. |

## Output

`--output table` (default) prints a concise human view; `--output json` prints the full result objects, suitable for piping into `jq` or other tooling.

## Exit codes

`0` on success; `1` when a command throws (for example, an unknown platform, an unresolvable scope, or a live call failure). Warnings are written to standard error and prefixed `warning:`.
