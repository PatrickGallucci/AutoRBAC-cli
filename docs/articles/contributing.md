# Contributing

## Build & test

```bash
dotnet build           # build the solution (AutoRbac.sln)
dotnet test            # run the xUnit suite (offline + live-path fakes)
dotnet run --project src/AutoRbac.Cli -- provider   # run the CLI
```

## Project layout

| Path | Contents |
|------|----------|
| `src/AutoRbac.Core/` | Reusable library: models, engine, providers, embedded JSON knowledge bases. |
| `src/AutoRbac.Cli/` | `System.CommandLine` front end (the `autorbac` executable / tool). |
| `tests/AutoRbac.Tests/` | xUnit suite, ported from the original Pester tests. |
| `docs/` | This DocFX documentation site. |

## Adding a platform provider

1. Implement `IRbacProvider` (or subclass `ProviderBase`) in `src/AutoRbac.Core/Providers/`.
2. Add the platform's commands to `Data/CommandRoleMap.json`.
3. Register it in `ProviderRegistry.CreateDefault()`.

No engine changes are required — the registry, scope resolver, and orchestration service are platform-agnostic.

## Building the docs locally

```bash
dotnet tool install -g docfx
docfx docs/docfx.json --serve     # builds to docs/_site and serves at http://localhost:8080
```

## Releasing

The [`Release` workflow](https://github.com/PatrickGallucci/AutoRBAC-cli/blob/main/.github/workflows/release.yml) runs on every `v*` tag: it tests, packs the `AutoRbac.Cli` tool, publishes it to NuGet.org, and creates a GitHub Release with the `.nupkg` attached.

Publishing uses **[NuGet Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing)** — keyless, OIDC-based, with no long-lived API key in the repo. The job requests a GitHub OIDC token and `NuGet/login@v1` exchanges it for a short-lived key. One-time setup:

1. On **nuget.org → Trusted Publishing**, add a policy: Repository Owner `PatrickGallucci`, Repository `AutoRBAC-cli`, Workflow File `release.yml`.
2. Add a repo secret **`NUGET_USER`** = your nuget.org profile name.
3. Tag a release: `git tag vX.Y.Z && git push origin vX.Y.Z`.

Until the secret is set, the publish step self-skips so the workflow stays green.

## Conventions

- Match the surrounding code's naming, nullability, and comment density.
- Keep the knowledge base as pure data; encode behaviour in providers.
- Add or update an xUnit test for every behavioural change.
