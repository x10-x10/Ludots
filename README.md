# Ludots

**SuperFastECSGameplayFramework** - A high-performance, data-oriented gameplay framework built on Arch ECS.

[![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue.svg)](LICENSE)

[中文文档 (Chinese)](README_CN.md)

## Introduction

Ludots is a modern C# game framework designed for high-performance gameplay logic. It leverages ECS (Entity Component System) architecture, deterministic simulation, and a modular design to support complex game genres like MOBA, RTS, and Simulation games.

## Key Features

*   **High-Performance ECS**: Built on [Arch](https://github.com/genaray/Arch), optimized for speed and memory efficiency.
*   **Gameplay Ability System (GAS)**: A robust ability system inspired by UE GAS, supporting attributes, effects, and tags.
*   **Modular Architecture**: Fully moddable with a Virtual File System (VFS) and hot-reloadable configurations.
*   **Advanced Navigation**: Integrated 2D navigation with NavMesh, FlowField, and local avoidance (ORCA).
*   **Deterministic Simulation**: Fixed-point math and deterministic scheduling for reliable networking and replay.
*   **Visual Editor**: React-based visual editor for map editing and debugging.

## Project Conventions

*   **Mod build**: Dev-as-release. All mods output to `bin/net8.0/` (no Debug/Release split). See `docs/developer-guide/02_mod_architecture.md`.
*   **Documentation**: Follow `docs/developer-guide/00_documentation_standards.md`. No internal milestones or ticket IDs in docs.
*   **Testing**: AAA pattern, NUnit. See `src/Tests/GasTests/TESTING_STYLE.md`.

## Quick Start

### Prerequisites
*   .NET 8.0 SDK or later
*   Node.js & npm (for Editor)

### Build & Run

**Using Convenience Scripts (Recommended)**

Scripts are located in the `scripts/` directory:

```bash
# Run the Visual Editor (Web + Bridge)
.\scripts\run-editor.cmd

# Run the Mod Launcher
.\scripts\run-mod-launcher.cmd
```

**Manual Build (CLI)**

```bash
# Build the main Raylib App
dotnet build .\src\Apps\Raylib\Ludots.App.Raylib\Ludots.App.Raylib.csproj -c Release

# Run Navigation2D Playground
dotnet run --project .\src\Apps\Raylib\Ludots.App.Raylib\Ludots.App.Raylib.csproj -c Release -- game.navigation2d.json
```

## Project Structure

*   `src/Core`: The heart of the engine (ECS, GAS, Physics, Math).
*   `src/Apps`: Application entry points (Desktop/Raylib, Web).
*   `src/Mods`: Built-in mods and examples (MobaDemo, RtsDemo). Each has `mod.json` and outputs to `bin/net8.0/`.
*   `src/Tools`: Developer tools (Editor, ModLauncher, NavBake).
*   `assets`: Game assets and configurations.
*   `docs`: Comprehensive documentation.

## Documentation

*   [Developer Guide](docs/developer-guide/README.md) — Architecture, Mod system, GAS, CLI.
*   [Architecture Guide](docs/arch-guide/README.md) — External Arch ECS references.
*   [Audits](docs/audits/) — Phase reports, merge plans, E2E acceptance.

## Contributing

This project is licensed under the **AGPL-3.0 License**. If you use this code in a distributed project (including over a network), you must open source your project under the same AGPL license.

## License

This project is licensed under the **GNU Affero General Public License v3.0 (AGPL-3.0)** - see the [LICENSE](LICENSE) file for details.

---

## Acknowledgments & Third-Party Libraries

We gratefully acknowledge the following open-source projects that make Ludots possible.

### Core Dependencies

| Library | License | Usage & Modifications | Source |
| :--- | :--- | :--- | :--- |
| **Arch** | MIT | **Core ECS**. Integrated as source in `src/Libraries/Arch`. Critical high-performance ECS backend. | [genaray/Arch](https://github.com/genaray/Arch) |
| **Arch.Extended** | MIT | **ECS Utilities**. Source integrated. Provides additional ECS query and batching tools. | [genaray/Arch.Extended](https://github.com/genaray/Arch.Extended) |
| **DotRecast** | MIT | **Navigation**. Source integrated in `src/Libraries/DotRecast`. Used for NavMesh generation and pathfinding (Recast & Detour C# port). | [ikpil/DotRecast](https://github.com/ikpil/DotRecast) |
| **Raylib-cs** | Zlib | **Rendering**. Source integrated in `src/Libraries/Raylib-cs`. C# bindings for Raylib, used for the desktop client rendering. | [ChrisDill/Raylib-cs](https://github.com/ChrisDill/Raylib-cs) |
| **FixPointCS** | MIT | **Math**. Source integrated in `external/FixPointCS-master`. Deterministic fixed-point mathematics for simulation consistency. | [asik/FixPointCS](https://github.com/asik/FixPointCS) |

*Disclaimer: All trademarks and registered trademarks are the property of their respective owners.*
