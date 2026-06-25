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

## Conventions

- Match the surrounding code's naming, nullability, and comment density.
- Keep the knowledge base as pure data; encode behaviour in providers.
- Add or update an xUnit test for every behavioural change.
