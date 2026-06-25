# Installation

AutoRBAC builds with the **.NET 8 SDK**. There are three ways to run it.

## Run from source

```bash
git clone https://github.com/PatrickGallucci/AutoRBAC-cli.git
cd AutoRBAC-cli
dotnet build
dotnet run --project src/AutoRbac.Cli -- provider
```

## Publish a self-contained binary

Produces a single executable with no runtime prerequisite:

```bash
dotnet publish src/AutoRbac.Cli -c Release -r win-x64 --self-contained \
    -p:PublishSingleFile=true -o ./dist
# -> ./dist/autorbac(.exe)
```

Swap `-r` for `linux-x64`, `osx-arm64`, etc. as needed.

## Install as a .NET tool

The CLI packs as a .NET tool whose command is `autorbac`.

```bash
# From NuGet (once published):
dotnet tool install -g AutoRbac.Cli
autorbac --help

# From a locally built package:
dotnet pack src/AutoRbac.Cli -c Release -o ./artifacts
dotnet tool install -g AutoRbac.Cli --add-source ./artifacts
```

Update or remove later with `dotnet tool update -g AutoRbac.Cli` / `dotnet tool uninstall -g AutoRbac.Cli`.

## SDK pinning

The repo pins the SDK feature band via `global.json` (`8.0.4xx`). CI installs the exact SDK with `actions/setup-dotnet` using `global-json-file`.
