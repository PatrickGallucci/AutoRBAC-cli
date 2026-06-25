# Commands

Run `autorbac <command> --help` for the authoritative option list. Global to every command: `--output table|json` (default `table`).

## `provider`

List the registered platform providers (name, aliases, live-probe support).

```bash
autorbac provider [--platform <name|alias>]
```

## `requirement`

Resolve the minimum role(s)/permissions a command requires (preflight, non-destructive).

| Option | Required | Description |
|--------|:--------:|-------------|
| `--platform`, `-p` | ✅ | Platform name or alias. |
| `--command`, `-c` | ✅ | Command / operation to evaluate. |
| `--map-path` | | Alternate knowledge-base JSON. |
| `--tenant-id`, `--live` | | Probe-identity context. |

## `test-access`

Test whether a caller holds the required role(s) at a scope.

| Option | Required | Description |
|--------|:--------:|-------------|
| `--platform`, `-p` | ✅ | Platform name or alias. |
| `--caller-id` | ✅ | Identity being evaluated. |
| `--required-role` | ✅ | Role(s) expected. Repeatable. |
| `--scope` | | Scope to evaluate at. |
| `--role-assignment` | | Offline assignment `Role` or `Role@Scope`. Repeatable. |
| `--workspace-id`, `--collection`, `--endpoint` | | Provider options. |
| `--tenant-id`, `--live` | | Probe-identity context. |

## `grant-script`

Generate idempotent grant + revoke snippets for a role.

| Option | Required | Description |
|--------|:--------:|-------------|
| `--platform`, `-p` | ✅ | Platform name or alias. |
| `--caller-id` | ✅ | Identity to grant/revoke. |
| `--role` | ✅ | Role to assign. |
| `--scope` | ✅ | Scope the assignment applies to. |
| `--workspace-id`, `--collection`, `--endpoint` | | Provider options. |

## `probe`

Flagship: resolve the requirement (preflight or live) **and** the caller's access in one call.

| Option | Required | Description |
|--------|:--------:|-------------|
| `--platform`, `-p` | ✅ | Platform name or alias. |
| `--command`, `-c` | ✅ | Command being probed. |
| `--caller-id` | ✅ | Identity whose access is evaluated. |
| `--scope` / `--subscription-id` / `--resource-group-name` / `--management-group-id` / `--resource-id` | | Scope, or the parts to build an ARM scope. |
| `--live-probe`, `--error-text` | | Derive the Azure requirement from an `AuthorizationFailed`. |
| `--role-assignment` | | Offline assignment(s). Repeatable. |
| `--map-path`, `--workspace-id`, `--collection`, `--endpoint` | | Knowledge base / provider options. |
| `--tenant-id`, `--live` | | Probe-identity context. |

## `set-access`

Discover, evaluate, and (with `--apply`) grant the least-privilege roles a command needs.

Same options as `probe`, plus:

| Option | Description |
|--------|-------------|
| `--apply` | Grant missing roles (Azure only). Without it, report only (the safe default). |
