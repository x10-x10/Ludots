# Launcher CLI Runbook

本文定义 Ludots 启动器的规范 CLI 体验。产品面只保留一条启动主线:

- 可视化 launcher: `.\scripts\run-mod-launcher.cmd`
- CLI launcher: `.\scripts\run-mod-launcher.cmd cli ...`

两条入口都复用同一套 backend: `src/Tools/Ludots.Launcher.Backend/LauncherService.cs`，并通过 `src/Tools/Ludots.Editor.Bridge/Program.cs` 暴露给 web launcher。

## 1 状态文件

启动器状态拆成四类文件，职责不混用:

- `launcher.config.json`
  仓库级扫描根、binding、默认 adapter、project hint。`mod` 可以在任意路径，只要落在 `scanRoots` 内，或者被 `binding set --path ...` / `path:...` 显式指定。
- `launcher.presets.json`
  仓库级预设。保存 selector 组合、adapter 和 build mode。
- `%AppData%/Ludots/Launcher/preferences.json`
  用户偏好。只保存最近一次选择的 adapter / preset。
- `%AppData%/Ludots/Launcher/config.overlay.json`
  用户覆盖层。用于本机追加 scan root、binding 或 project hint，不污染仓库配置。

运行时 bootstrap 不走以上三类文件:

- `launcher.runtime.json`
  启动时自动写到目标 adapter 输出目录，只包含 `ModPaths`。
- `game.json`
  变成可选调试入口。只有你绕过 launcher、直接运行 adapter app 时才需要手工传入；产品路径默认用 `launcher.runtime.json`。

实际运行配置仍然来自 `src/Core/Config/ConfigPipeline.cs` 对 `assets/Configs/game.json`、`<Mod>/assets/game.json`、`<Mod>/assets/Configs/game.json` 的合并。

## 2 Selector 模型

CLI 统一接受 selector，而不是只接受固定 `mods/` 目录里的 mod id。

支持的 selector:

```text
$camera_acceptance
camera_acceptance
mod:CameraAcceptanceMod
path:mods/fixtures/camera/CameraAcceptanceMod
preset:camera_acceptance_web
```

规则:

- `$alias`
  直接命中 `launcher.config.json` 里的 binding。
- `alias`
  PowerShell 友好的 binding 简写。如果同名 binding 存在，优先解析成 `$alias`；否则解析成 `mod:<id>`。
- `mod:<ModId>`
  直接按 manifest id 解析。
- `path:<mod-root>`
  直接指向任意 mod 根目录，不要求它位于 `mods/`。
- `preset:<presetId>`
  复用预设里的 selector 集合。

一个 `launch` / `resolve` 可以接收多个 selector。启动器会自动补齐依赖、导出 SDK ref dll，并对最终启动配置做显式诊断。

## 3 常用命令

### 3.1 查看启动计划

```powershell
.\scripts\run-mod-launcher.cmd cli resolve camera_acceptance --adapter raylib
.\scripts\run-mod-launcher.cmd cli resolve camera_acceptance nav_playground --adapter web
.\scripts\run-mod-launcher.cmd cli resolve --mod CameraAcceptanceMod --mod Navigation2DPlaygroundMod --adapter raylib --json
```

`resolve` 会输出:

- `rootMods`: 用户显式选择的 root mod
- `orderedMods`: 自动补齐依赖后的真实加载顺序
- `startup`:
  - `defaultCoreMod`
  - `startupMapId`
  - `startupInputContexts`
- `warnings`: 多 root mod 时哪个 `startupMapId` 最终生效，以及生效来源文件

多 mod 同时提供 `startupMapId` 时，运行时仍然只会启动一个 map。以 `orderedMods` 中最后写入对应字段的 fragment 为准，所以先跑 `resolve` 再 `launch` 是规范体验。

### 3.2 启动游戏

```powershell
.\scripts\run-mod-launcher.cmd cli launch camera_acceptance --adapter raylib
.\scripts\run-mod-launcher.cmd cli launch nav_playground --adapter web
.\scripts\run-mod-launcher.cmd cli launch camera_acceptance nav_playground --adapter raylib
.\scripts\run-mod-launcher.cmd cli launch camera_acceptance nav_playground --adapter web
```

行为约定:

- `--adapter raylib|web` 显式指定适配层；不传时回落到用户偏好或仓库默认 adapter。
- 依赖 mod、主 dll、ref dll、graph compile 都由 launcher backend 自动处理。
- 所有 mod 开发者和玩家都走同一条启动链路，不再要求手工 `gamejson write`。

### 3.3 录制启动证据

```powershell
.\scripts\run-mod-launcher.cmd cli launch camera_acceptance --adapter raylib --record artifacts/acceptance/launcher-camera-acceptance-raylib
.\scripts\run-mod-launcher.cmd cli launch nav_playground --adapter web --record artifacts/acceptance/launcher-nav-playground-web
```

`--record` 会生成多帧截图、摘要和签名。当前 cross-adapter 证据见:

- `artifacts/acceptance/launcher-camera-acceptance-raylib`
- `artifacts/acceptance/launcher-camera-acceptance-web`
- `artifacts/acceptance/launcher-nav-playground-raylib`
- `artifacts/acceptance/launcher-nav-playground-web`

### 3.4 管理工作区和 binding

```powershell
.\scripts\run-mod-launcher.cmd cli workspace list
.\scripts\run-mod-launcher.cmd cli workspace add --path ..\ExternalMods

.\scripts\run-mod-launcher.cmd cli binding list
.\scripts\run-mod-launcher.cmd cli binding set camera_acceptance --path mods/fixtures/camera/CameraAcceptanceMod --project CameraAcceptanceMod.csproj
.\scripts\run-mod-launcher.cmd cli binding set nav_playground --path mods/Navigation2DPlaygroundMod --project Navigation2DPlaygroundMod.csproj
```

说明:

- `workspace add` 会把目录加到 `launcher.config.json` 的 `scanRoots`，递归扫描其中所有 `mod.json`。
- `binding set --path ...` 显式建立“变量名 -> 路径”的映射，适合放仓库内外任意位置的 mod。
- `--project` 用于指定 csproj；launcher 仍会自动解析依赖和主 dll。

### 3.5 管理预设

```powershell
.\scripts\run-mod-launcher.cmd cli preset list
.\scripts\run-mod-launcher.cmd cli preset save --name camera-web camera_acceptance --adapter web
.\scripts\run-mod-launcher.cmd cli preset save --name camera-nav-raylib camera_acceptance nav_playground --adapter raylib
.\scripts\run-mod-launcher.cmd cli preset select preset_camera-nav-raylib
```

预设保存的是 selector 组合，不是展开后的固定 mod 列表，因此依赖和 binding 变化会在下次 `resolve`/`launch` 时自动重新计算。

### 3.6 构建、SDK 和工程辅助

```powershell
.\scripts\run-mod-launcher.cmd cli build camera_acceptance --adapter raylib
.\scripts\run-mod-launcher.cmd cli build app --adapter web
.\scripts\run-mod-launcher.cmd cli sdk export
.\scripts\run-mod-launcher.cmd cli mod fix-project CameraAcceptanceMod
.\scripts\run-mod-launcher.cmd cli mod solution CameraAcceptanceMod
```

说明:

- `build` 仍然可用，但只是 `launch` 的子流程，不再是普通用户的主路径。
- `sdk export` 会导出 mod SDK 和 ref dll，保证开发态与玩家启动态共用一套产物约定。

## 4 Web Launcher 与 CLI 的关系

`.\scripts\run-mod-launcher.cmd` 不带 `cli` 时，会:

1. 构建 `src/Tools/Ludots.Launcher.React`
2. 确保 `src/Tools/Ludots.Editor.Bridge/Ludots.Editor.Bridge.csproj` 已启动
3. 打开 `http://localhost:5299/launcher/`

Bridge 的 `/api/launch` 直接调用 `LauncherService.LaunchAsync(...)`。因此 web launcher 和 CLI 复用同一套:

- selector 解析
- dependency closure
- startup diagnostics
- SDK / ref dll 导出
- adapter 启动逻辑

## 5 直接调试 adapter app

如果你需要绕过 launcher 直接调试 adapter 进程，可以手工传 bootstrap 文件:

```powershell
dotnet run --project src/Apps/Raylib/Ludots.App.Raylib/Ludots.App.Raylib.csproj -c Release -- launcher.runtime.json
dotnet run --project src/Apps/Web/Ludots.App.Web/Ludots.App.Web.csproj -c Release -- launcher.runtime.json
```

这里的 `launcher.runtime.json` 仍然只负责 `ModPaths`。真正的 `startupMapId`、`defaultCoreMod`、`startupInputContexts` 仍然由 `ConfigPipeline` 从 Core + Mods 合并得到。

## 6 相关文档

- [开发环境与构建](../conventions/03_environment_setup.md)
- [启动顺序与入口点](../architecture/startup_entrypoints.md)
- [统一 launcher CLI RFC](../rfcs/RFC-0001-unified-launcher-cli-and-workspace.md)
