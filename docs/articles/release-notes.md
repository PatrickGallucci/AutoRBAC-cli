# Release Notes

The full, authoritative changelog lives in [`CHANGELOG.md`](https://github.com/PatrickGallucci/AutoRBAC-cli/blob/main/CHANGELOG.md) and follows [Keep a Changelog](https://keepachangelog.com/) + [SemVer](https://semver.org/).

## 2.0.0 — 2026-06-24

**Breaking:** rewritten from a PowerShell module into a cross-platform **.NET 8 CLI** (`autorbac`) on `System.CommandLine`, plus a reusable `AutoRbac.Core` library. The PowerShell module is removed.

- The six cmdlets become subcommands: `probe`, `requirement`, `test-access`, `grant-script`, `set-access`, `provider`. Add `--output json` for machine-readable output.
- **Live probing** is now `--live-probe --error-text "<AuthorizationFailed>"`; `--live` opts into live Azure SDK / REST calls.
- Offline assignments are supplied with `--role-assignment "Role"` / `"Role@Scope"`. `set-access --apply` replaces `-WhatIf`/`-Confirm` (report-only is the default).
- **Added:** live Azure provider over `Azure.ResourceManager.Authorization` + `Azure.Identity`; embedded JSON knowledge bases (`--map-path` override); xUnit test suite (51 tests) with in-memory Azure/REST fakes.
- **Removed:** PSFramework logging, the MkDocs site, PowerShell run-as parameters, and the `examples/*.ps1` scripts.

## Earlier (PowerShell module)

`1.1.0`, `1.0.0`, and `0.1.0` were releases of the original `PSAutoRBAC` PowerShell module. See the changelog for the full history and the cmdlet-to-subcommand migration map.
