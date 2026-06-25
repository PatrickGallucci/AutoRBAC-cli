# Usage

## Offline vs. live

AutoRBAC evaluates access in one of two modes:

- **Offline (default)** — you supply the caller's assignments with `--role-assignment`. No network calls. Deterministic; ideal for CI and dry-runs.
- **Live** — add `--live` to evaluate against the real directory using `DefaultAzureCredential`. In live mode `--caller-id` is the principal **object id** (ARM works on object ids, not UPNs).

```bash
# Offline:
autorbac test-access --platform Azure --caller-id dev@contoso.com \
    --required-role Reader --scope "/subscriptions/$SUB" \
    --role-assignment "Reader@/subscriptions/$SUB"

# Live (real Azure role-assignment lookup):
autorbac test-access --platform Azure --caller-id <object-id> \
    --required-role Reader --scope "/subscriptions/$SUB" --live
```

`--role-assignment` accepts a bare role (`"Reader"`, treated as held at the queried scope) or a `Role@Scope` pair, and is repeatable.

## The flagship probe

`probe` resolves the requirement and the caller's access in one call, attaching grant/revoke snippets per required role:

```bash
autorbac probe --platform Azure --command New-AzResourceGroup \
    --caller-id dev@contoso.com --subscription-id $SUB \
    --role-assignment "Reader@/subscriptions/$SUB"
```

Scope can be given explicitly (`--scope`) or built from `--subscription-id` / `--resource-group-name` / `--management-group-id` / `--resource-id`.

## Live probe (Azure only)

Only Azure's authorization error names the missing action. Run the real operation, then feed AutoRBAC the error text to derive the exact role:

```bash
autorbac probe --platform Azure --command New-AzStorageAccount \
    --caller-id dev@contoso.com --scope "/subscriptions/$SUB/resourceGroups/rg" \
    --live-probe --error-text "does not have authorization to perform action 'Microsoft.Storage/storageAccounts/write' over scope '/subscriptions/$SUB/resourceGroups/rg' (Code: AuthorizationFailed)"
# -> Role: Storage Account Contributor
```

Using `--live-probe` on a non-Azure platform warns and falls back to preflight resolution.

## End-to-end grants

`set-access` discovers, evaluates, and (with `--apply`) grants. Report-only is the default:

```bash
# Report only:
autorbac set-access --platform Azure --subscription-id $SUB \
    --caller-id dev@contoso.com --command New-AzResourceGroup \
    --role-assignment "Reader@/subscriptions/$SUB"

# Grant missing Azure roles for real (needs --live + roleAssignments/write):
autorbac set-access --platform Azure --subscription-id $SUB \
    --caller-id <object-id> --command New-AzResourceGroup --live --apply
```

Automatic granting is implemented for **Azure**. For Graph / Fabric / Purview the `AddScript` is returned for review and `Applied` stays `false`.

## Other platforms

```bash
# Graph directory-role requirement:
autorbac requirement --platform "Microsoft Graph" --command New-MgGroup

# Fabric workspace role hierarchy (Admin satisfies Contributor):
autorbac test-access --platform Fabric --caller-id <oid> \
    --required-role Contributor --scope <workspace-id> --role-assignment Admin

# Purview metadata-policy grant guidance:
autorbac grant-script --platform Purview --caller-id <oid> --role "Data Curator" \
    --scope https://acct.purview.azure.com --collection mycollection
```
