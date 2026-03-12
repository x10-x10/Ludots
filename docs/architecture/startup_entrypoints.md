# 启动顺序与入口点

本篇说明 Ludots 从 launcher 到 adapter app 再到首张地图的完整启动路径。当前产品级入口只保留 web launcher 和 CLI launcher，两者复用同一套 backend。

## 1 入口点一览

### 1.1 Launcher 包装脚本

- `scripts/run-mod-launcher.ps1`
  - 不带 `cli` 时: 构建 `src/Tools/Ludots.Launcher.React`，确保 `src/Tools/Ludots.Editor.Bridge/Ludots.Editor.Bridge.csproj` 存活，并打开 `/launcher/`
  - 带 `cli` 时: 转发到 `src/Tools/Ludots.Launcher.Cli/Program.cs`
- `scripts/run-mod-launcher.cmd`
  - Windows 包装器，原样转发参数到 `run-mod-launcher.ps1`

规范命令只有两种:

```powershell
.\scripts\run-mod-launcher.cmd
.\scripts\run-mod-launcher.cmd cli <command> ...
```

`src/Tools/ModLauncher` 中的 WPF launcher 已降级为遗留兼容层，不再作为新功能入口。

### 1.2 共享 launcher backend

共享 backend 位于 `src/Tools/Ludots.Launcher.Backend`，核心入口是 `LauncherService`:

- `Resolve(...)`
  负责 selector 解析、binding / preset 展开、依赖闭包和启动诊断
- `BuildAsync(...)`
  负责 mod 构建、SDK 导出、graph compile
- `LaunchAsync(...)`
  负责 adapter app 构建、写入 `launcher.runtime.json`、启动目标平台

web launcher 通过 `src/Tools/Ludots.Editor.Bridge/Program.cs` 的 `/api/launch` 复用同一个 `LauncherService.LaunchAsync(...)`，CLI 直接调用同一个 service。

### 1.3 Adapter app 入口

- Raylib: `src/Apps/Raylib/Ludots.App.Raylib/Program.cs`
- Web: `src/Apps/Web/Ludots.App.Web/Program.cs`

两个 app 的首参都支持显式传入 bootstrap 文件；未传时默认读取 `launcher.runtime.json`。

## 2 Launcher 规划阶段

launcher 启动流程从 selector 开始，而不是从固定 `mods/` 目录开始。

### 2.1 Selector 解析

`src/Tools/Ludots.Launcher.Cli/Program.cs` 和 bridge 请求体都支持以下 selector:

- `$alias`
- `alias`
- `mod:<ModId>`
- `path:<mod-root>`
- `preset:<presetId>`

其中:

- binding 显式存放在 `launcher.config.json`
- preset 显式存放在 `launcher.presets.json`
- 用户 adapter / preset 偏好存放在 `%AppData%/Ludots/Launcher/preferences.json`

### 2.2 Mod 发现与依赖闭包

`LauncherService.ResolvePlan(...)` 会:

1. 合并 `launcher.config.json` 和用户 overlay
2. 按 `scanRoots` 递归扫描 `mod.json`
3. 应用 binding，把别名解析到 `path` 或 `modid`
4. 展开 preset
5. 解析 root mod
6. 自动补齐依赖，生成 `orderedMods`

这意味着:

- mod 可以放在任意路径
- binding 可以稳定映射“全局变量名 -> 路径”
- 递归扫描、依赖解析、project hint 都在 backend 一处实现

### 2.3 启动诊断

`LauncherService.ResolvePlan(...)` 还会读取:

- `assets/Configs/game.json`
- `<Mod>/assets/game.json`
- `<Mod>/assets/Configs/game.json`

并在 `LauncherLaunchPlan.Diagnostics` 中给出:

- `defaultCoreMod`
- `startupMapId`
- `startupInputContexts`
- 多 root mod 冲突警告
- 最终生效来源文件

因此 web launcher 和 CLI 都可以展示完全一致的“最终启动组合”。

## 3 Bootstrap 文件职责

### 3.1 `launcher.runtime.json`

launcher 产出的 bootstrap 文件只有一个职责: 为 adapter app 提供 `ModPaths`。

对应实现:

- 写入: `src/Tools/Ludots.Launcher.Backend/LauncherService.cs`
- 读取: `src/Core/Hosting/GameBootstrapper.cs`

文件不承载 gameplay 配置，也不保存用户偏好或 preset。

### 3.2 `game.json`

`game.json` 变成可选入口:

- 如果通过 launcher 启动: 默认不需要手工写 `game.json`
- 如果直接调试 adapter app: 可以自己传一个符合 `AppBootstrapConfig` 的 bootstrap 文件

真正的运行配置仍然来自 `ConfigPipeline` 合并后的 `GameConfig`。

## 4 从 launcher 到进入第一张地图

### 4.1 Launcher 阶段

1. `LauncherService.ResolvePlan(...)` 得到 `rootMods`、`orderedMods` 和 `Diagnostics`
2. `BuildPlannedModsAsync(...)` 自动构建 source mod，导出 ref dll
3. `BuildAppAsync(...)` 构建目标 adapter app
4. `WriteRuntimeBootstrap(...)` 写出 `launcher.runtime.json`
5. `LaunchAsync(...)` 启动 Raylib 或 Web app

### 4.2 App bootstrap 阶段

`src/Core/Hosting/GameBootstrapper.cs` 的主流程:

1. 从 base directory 往上找到仓库 `assets/`
2. 读取 `launcher.runtime.json`，仅解析 `ModPaths`
3. 校验每个 mod 路径都存在 `mod.json`
4. 调用 `GameEngine.InitializeWithConfigPipeline(modPaths, assetsRoot)`

### 4.3 Engine 初始化阶段

在 `GameEngine.InitializeWithConfigPipeline(...)` 内:

1. 挂载 Core VFS
2. 初始化 FunctionRegistry、TriggerManager、SystemFactoryRegistry、TriggerDecoratorRegistry、ModLoader、MapManager
3. 按依赖顺序加载 mods
4. 用 `ConfigPipeline.MergeGameConfig()` 合并最终 `GameConfig`
5. 初始化 ECS World、空间服务、GameSession、系统组

### 4.4 进入首张地图

adapter host 读取合并后的 `GameConfig.StartupMapId`:

- Raylib host: `src/Adapters/Raylib/Ludots.Adapter.Raylib`
- Web host: `src/Adapters/Web/Ludots.Adapter.Web`

然后执行:

1. `engine.Start()`
2. `engine.LoadMap(config.StartupMapId)`
3. 进入各自平台主循环

如果多 root mod 同时写入了 `startupMapId`，运行时仍然只会进入一个 map。最终 winner 由 launcher 规划阶段的 `Diagnostics` 明确输出。

## 5 直接调试 adapter

两个 adapter app 都允许直接传 bootstrap 文件:

```powershell
dotnet run --project src/Apps/Raylib/Ludots.App.Raylib/Ludots.App.Raylib.csproj -c Release -- launcher.runtime.json
dotnet run --project src/Apps/Web/Ludots.App.Web/Ludots.App.Web.csproj -c Release -- launcher.runtime.json
```

这条路径适合 IDE 调试，但不改变产品规范: 普通启动仍然通过 launcher CLI 或 web launcher 完成。

## 6 相关文档

- [Launcher CLI Runbook](../reference/cli_runbook.md)
- [开发环境与构建](../conventions/03_environment_setup.md)
- [ConfigPipeline 合并管线](config_pipeline.md)
- [Mod 运行时单一事实源](mod_runtime_single_source_of_truth.md)
