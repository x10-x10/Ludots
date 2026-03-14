# Startup Entrypoints

This document describes the end-to-end startup path from launcher entrypoint to adapter app and first map load.

Current product entrypoints:

- web launcher
- launcher CLI

Both reuse the same backend.

## 1. Wrapper Scripts

- `scripts/run-mod-launcher.ps1`
  - without `cli`: build the web launcher, ensure the bridge is alive, open the launcher URL
  - with `cli`: forward to `src/Tools/Ludots.Launcher.Cli/Program.cs`
- `scripts/run-mod-launcher.cmd`
  - Windows wrapper that forwards arguments to `run-mod-launcher.ps1`

Canonical commands:

```powershell
.\scripts\run-mod-launcher.cmd
.\scripts\run-mod-launcher.cmd cli <command> ...
```

Legacy WPF launcher under `src/Tools/ModLauncher` is no longer the product path.

## 2. Shared Launcher Backend

Shared backend lives in `src/Tools/Ludots.Launcher.Backend`.

Core responsibilities:

- resolve selectors, bindings, presets, and dependency closure
- surface startup diagnostics
- build required mods and adapter apps
- export SDK ref DLLs
- write `launcher.runtime.json`
- launch the chosen adapter

CLI calls the backend directly.

Web launcher calls the same backend through:

- `src/Tools/Ludots.Editor.Bridge/Program.cs`

## 3. Resolve Stage

Selector inputs supported by the launcher:

- `$alias`
- `alias`
- `mod:<ModId>`
- `path:<mod-root>`
- `preset:<presetId>`

Resolve behavior:

1. load repository config and user overlay
2. scan configured roots recursively for `mod.json`
3. expand bindings and presets
4. resolve root mods
5. compute ordered dependency closure
6. compute startup diagnostics

Startup diagnostics include:

- `defaultCoreMod`
- `startupMapId`
- `startupInputContexts`
- conflict warnings for multi-root launches

## 4. Bootstrap Files

### 4.1 `launcher.runtime.json`

This is the runtime bootstrap written by the launcher.

Responsibilities:

- provide adapter bootstrap data such as `ModPaths`

It is intentionally not the long-term store for preferences, presets, or repository scan config.

### 4.2 `game.json`

`game.json` is optional.

- product launcher path: not required
- direct adapter debug path: still allowed as an explicit bootstrap file

Gameplay configuration still comes from the merged config pipeline over core assets plus selected mods.

## 5. Adapter App Stage

Adapter entrypoints:

- Raylib: `src/Apps/Raylib/Ludots.App.Raylib/Program.cs`
- Web: `src/Apps/Web/Ludots.App.Web/Program.cs`

Both adapter apps accept an explicit bootstrap file path. If none is provided, they read `launcher.runtime.json`.

## 6. Engine Initialization Stage

`src/Core/Hosting/GameBootstrapper.cs` and `src/Core/Engine/GameEngine.cs` handle:

1. locating repository assets
2. validating mod roots from bootstrap
3. initializing the config pipeline
4. loading mods in dependency order
5. merging final `GameConfig`
6. starting systems and loading the selected startup map

Only one startup map is entered at runtime even for multi-root launch. The winning source is surfaced by launcher diagnostics.

## 7. Direct Debug Path

```powershell
dotnet run --project src/Apps/Raylib/Ludots.App.Raylib/Ludots.App.Raylib.csproj -c Release -- launcher.runtime.json
dotnet run --project src/Apps/Web/Ludots.App.Web/Ludots.App.Web.csproj -c Release -- launcher.runtime.json
```

This path is useful for debugger attachment, but it does not replace the product launcher contract.

## 8. Related Docs

- [Launcher CLI Runbook](../reference/cli_runbook.md)
- [Environment Setup](../conventions/03_environment_setup.md)
- [Unified Launcher RFC](../rfcs/RFC-0001-unified-launcher-cli-and-workspace.md)
