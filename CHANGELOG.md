# Changelog

All notable changes to **AutoRBAC** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- **CI now publishes to NuGet via Trusted Publishing** (keyless OIDC) instead of a
  stored `NUGET_API_KEY`. The release workflow requests a GitHub OIDC token and
  `NuGet/login@v1` exchanges it for a short-lived key. Requires a one-time
  nuget.org Trusted Publishing policy and a `NUGET_USER` repo secret (see README).

## [2.0.0] - 2026-06-24

### Changed (breaking)

- **Rewritten from a PowerShell module into a cross-platform .NET 8 CLI** (`autorbac`),
  built on `System.CommandLine`, plus a reusable `AutoRbac.Core` library. The
  PowerShell module (`.psm1`/`.psd1`, `Public/`, `Private/`, `Providers/`) is removed.
- The six cmdlets become subcommands: `Invoke-RBACProbe` → `probe`,
  `Get-RBACRequirement` → `requirement`, `Test-RBACAccess` → `test-access`,
  `New-RBACGrantScript` → `grant-script`, `Set-RBACAccess` → `set-access`,
  `Get-RBACProvider` → `provider`. Add `--output json` for machine-readable output.
- **Live probing** is now driven by `--live-probe --error-text "<AuthorizationFailed>"`
  (the CLI cannot execute arbitrary Az cmdlets), preserving the action-parsing and
  least-privilege role-mapping logic. `--live` opts into live Azure SDK / REST calls.
- Offline assignments are supplied with `--role-assignment "Role"` or `"Role@Scope"`
  (replaces `-RoleAssignment`). `set-access --apply` replaces `-WhatIf`/`-Confirm`
  (report-only is the default).

### Added

- **Live Azure provider** over `Azure.ResourceManager.Authorization` + `Azure.Identity`
  (`DefaultAzureCredential`): role-assignment / definition lookups and `--apply` grants.
- Knowledge bases ship as embedded JSON (`Data/CommandRoleMap.json`,
  `Data/RoleActionMap.json`); `--map-path` overrides the command map.
- xUnit test suite (51 tests) ported from the original Pester suite, with in-memory
  Azure/REST fakes for the live paths.

### Removed

- PSFramework dependency and `Write-PSFMessage` logging, the MkDocs site, the
  PowerShell run-as parameters, and the `examples/*.ps1` provisioning scripts.

## [1.1.0] - 2026-06-24

### Added

- **PSFramework structured logging** throughout the module (`Write-PSFMessage` at
  Verbose / Debug / Significant / Warning / Error levels). PSFramework is now a
  required module (auto-installed by `Install-Module PSAutoRBAC`). Run with
  `-Verbose` / `-Debug` for detailed flow, or configure PSFramework logging
  providers to persist logs. See the new **Logging** docs page.
- Self-healing provisioning examples under `examples/`: Azure Storage blob,
  Entra group, Fabric item, and Purview asset - each catches an authorization
  failure, resolves the requirement with PSAutoRBAC, and retries.
- Knowledge-base entries for `Set-AzStorageBlobContent` and
  `New-AzStorageContainer` (data-plane `Storage Blob Data Contributor`).
- Documentation site pages: **Examples**, **Logging**, and **Contributing**.

### Fixed

- Publish workflow now stages the `Providers/` directory (previously omitted,
  which would have produced a broken package on the gallery).
- StrictMode-safe property access for non-web REST exceptions, error records
  without `ErrorDetails`, and Az contexts lacking an `Account`/`Tenant` property.

## [1.0.0] - 2026-06-23

A clean rewrite around a **per-platform provider model**. The single
"execute and parse the authorization error" idea only works on Azure (the one
platform whose error names the missing action); each platform is now resolved
with its best primitive instead.

### Changed (breaking)

- **Renamed the public surface.** Migration map:

  | 0.x | 1.0 |
  |-----|-----|
  | `Get-CommandRBACRequirement` | `Get-RBACRequirement` |
  | `Test-CallerRBACAssignment`  | `Test-RBACAccess` |
  | `New-RBACAssignmentScript`   | `New-RBACGrantScript` |
  | `Set-CallerRBACAssignment`   | `Set-RBACAccess` |

- All commands now take a `-Platform` parameter (name or alias) and dispatch to a
  registered provider. Output property `State` is renamed `HasAccess`; results
  carry `Platform`, `Source`, and `Permissions`.
- Scope inputs expanded: `-ManagementGroupId` and `-ResourceId` join
  `-SubscriptionId` / `-ResourceGroupName` / `-Scope`.

### Added

- **`Invoke-RBACProbe`** — flagship command that resolves a requirement and the
  caller's access in one call. `-LiveProbe` (Azure only, `ShouldProcess`-gated)
  executes the command and parses the live `AuthorizationFailed` to derive the
  required action → role.
- **`Get-RBACProvider`** — lists registered platforms and which support live probing.
- **Provider model** (`Providers/*.ps1` + `Register-RBACProvider`): Azure, Microsoft
  Graph, Microsoft Fabric, Microsoft Purview. Adding a platform is a single file.
  - Azure: KB + live `Get-AzRoleDefinition`; `AuthorizationFailed` parsing.
  - Graph: `Find-MgGraphCommand` resolution (its error is non-diagnostic).
  - Fabric: workspace `roleAssignments` REST; Admin > Member > Contributor > Viewer.
  - Purview: collection metadata-policy roles.
- **Run-as probe identity** — `-RunAsCredential` / `-RunAsServicePrincipal` /
  `-RunAsManagedIdentity` run the probe as a different identity than the ambient
  session, kept separate from the target `-CallerId`.
- `Data/RoleActionMap.psd1` — curated, offline Azure action → role reverse map.
- `PSScriptAnalyzerSettings.psd1` documenting the provider-contract lint posture.

### Notes

- Preflight is the default and is non-destructive on every platform. Automatic
  granting in `Set-RBACAccess` is implemented for Azure; for Graph/Fabric/Purview
  the `AddScript` is returned for review (`Applied = $false`).

## [0.1.0] - 2026-06-23

### Added

- Initial release of the PSAutoRBAC module.
- `Get-CommandRBACRequirement` — resolves the minimum RBAC role(s), control-plane
  actions, and scope level a platform command requires, from a data-driven
  knowledge base. Flags unknown commands with `IsKnown = $false`.
- `Test-CallerRBACAssignment` — tests whether a caller already holds the required
  role(s) at a scope (honouring scope inheritance); supports offline evaluation
  via `-RoleAssignment`.
- `New-RBACAssignmentScript` — generates idempotent grant and revoke PowerShell
  snippets for a role at a scope.
- `Set-CallerRBACAssignment` — end-to-end orchestration (discover → test →
  generate → apply) with `-WhatIf` / `-Confirm` support and an `-Args` alias.
- Knowledge base (`Data/CommandRoleMap.psd1`) seeded for Azure PowerShell and
  Microsoft Graph, with conservative defaults scaffolded for Microsoft Fabric
  and Microsoft Purview.
- Pester test suite (20 tests) covering manifest, public functions, parameter
  validation, scope resolution, and offline evaluation.
- MkDocs (Material) documentation site with command pages generated from
  comment-based help via `build/Update-Docs.ps1`.
- GitHub Actions workflows for CI (PSScriptAnalyzer + Pester), GitHub Pages
  deployment, and PowerShell Gallery publishing.

[Unreleased]: https://github.com/PatrickGallucci/PSAutoRBAC/compare/v2.0.0...HEAD
[2.0.0]: https://github.com/PatrickGallucci/PSAutoRBAC/compare/v1.1.0...v2.0.0
[1.1.0]: https://github.com/PatrickGallucci/PSAutoRBAC/compare/v0.1.0...v1.1.0
[1.0.0]: https://github.com/PatrickGallucci/PSAutoRBAC/compare/v0.1.0...v1.0.0
[0.1.0]: https://github.com/PatrickGallucci/PSAutoRBAC/releases/tag/v0.1.0
