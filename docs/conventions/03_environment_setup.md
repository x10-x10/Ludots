# 开发环境与构建

本篇汇总 Ludots 仓库的 SDK 要求、构建命令、测试命令、服务启动方式和平台特定说明。启动器规范以 `.\scripts\run-mod-launcher.cmd` 为准。

## 1 SDK 要求

| SDK | 必需 | 原因 |
|-----|------|------|
| .NET 8.0 | 是 | 主目标框架 |
| .NET 9.0 | 是 | DotRecast 多目标编译 |
| .NET 10.0 (preview) | 是 | DotRecast 多目标编译 |
| Node.js + npm | 是 | Web launcher 和 editor React 前端 |

缺少任一 .NET SDK 会导致 `dotnet restore` 失败。

### 1.1 Linux / Cloud VM 安装

SDKs 通过 `dotnet-install.sh` 安装到 `/usr/share/dotnet`，符号链接 `/usr/local/bin/dotnet`。`PATH` 和 `DOTNET_ROOT` 在 `~/.bashrc` 中设置。

## 2 常用构建与启动命令

```powershell
# 构建 launcher CLI
dotnet build src/Tools/Ludots.Launcher.Cli/Ludots.Launcher.Cli.csproj -c Release

# 构建 web bridge
dotnet build src/Tools/Ludots.Editor.Bridge/Ludots.Editor.Bridge.csproj -c Release

# 查看单 mod 启动计划
.\scripts\run-mod-launcher.cmd cli resolve camera_acceptance --adapter raylib

# 启动单 mod
.\scripts\run-mod-launcher.cmd cli launch camera_acceptance --adapter raylib

# 启动多 mod，并显式指定 web adapter
.\scripts\run-mod-launcher.cmd cli launch camera_acceptance nav_playground --adapter web

# 导出 mod SDK / ref dll
.\scripts\run-mod-launcher.cmd cli sdk export
```

约束:

- 规范包装命令是 `.\scripts\run-mod-launcher.cmd cli ...`，不要额外写 `-- cli ...`。
- selector 可以是 binding、`mod:<id>`、`path:<mod-root>`、`preset:<id>`，不再限制 mod 必须放在 `mods/`。
- `resolve` 是组合 mod 前的规范检查步骤；它会显示依赖闭包、最终 `startupMapId` 和来源文件。
- `launch` 会自动构建缺失依赖、导出 ref dll、写入 `launcher.runtime.json`，不再要求手工 `gamejson write`。
- `game.json` 只保留给直接调试 adapter app 的场景；产品路径默认使用 `launcher.runtime.json`。

## 3 测试命令

```powershell
# GAS 测试（核心 gameplay）
dotnet test src/Tests/GasTests/GasTests.csproj

# 3C / Camera 测试
dotnet test src/Tests/ThreeCTests/ThreeCTests.csproj

# Presentation 运行时测试
dotnet test src/Tests/PresentationTests/PresentationTests.csproj

# GAS 测试（详细输出）
dotnet test src/Tests/GasTests/GasTests.csproj --logger "console;verbosity=detailed"

# 运行指定测试类
dotnet test src/Tests/GasTests/GasTests.csproj --filter "FullyQualifiedName~TagRuleSetTests"

# 2D 导航测试
dotnet test src/Tests/Navigation2DTests/Navigation2DTests.csproj

# 架构边界测试
dotnet test src/Tests/ArchitectureTests/ArchitectureTests.csproj
```

测试框架: NUnit 4.2.2 + BenchmarkDotNet。测试风格见 `src/Tests/GasTests/TESTING_STYLE.md`。

## 4 服务启动

Ludots 当前的 launcher 产品面是 web launcher + CLI，共用 `src/Tools/Ludots.Launcher.Backend`。WPF `src/Tools/ModLauncher` 仅保留遗留兼容，不再承载新能力。

### 4.1 产品级 web launcher

```powershell
.\scripts\run-mod-launcher.cmd
```

该脚本会构建 `src/Tools/Ludots.Launcher.React`、拉起 `src/Tools/Ludots.Editor.Bridge/Ludots.Editor.Bridge.csproj`，然后打开 `http://localhost:5299/launcher/`。

### 4.2 前端开发模式 launcher

```powershell
.\scripts\run-launcher.cmd
```

该脚本启动:

- bridge: `http://localhost:5299`
- launcher vite dev server: `http://localhost:5174`

### 4.3 Editor React（地图编辑器）

```powershell
.\scripts\run-editor.cmd
```

或手工启动:

```powershell
dotnet run --project src/Tools/Ludots.Editor.Bridge/Ludots.Editor.Bridge.csproj
cd src/Tools/Ludots.Editor.React
npm run dev
```

### 4.4 直接调试 adapter app

```powershell
dotnet run --project src/Apps/Raylib/Ludots.App.Raylib/Ludots.App.Raylib.csproj -c Release -- launcher.runtime.json
dotnet run --project src/Apps/Web/Ludots.App.Web/Ludots.App.Web.csproj -c Release -- launcher.runtime.json
```

两者默认都读取 `launcher.runtime.json`。如果显式传入其它 bootstrap 文件，也必须满足 `src/Core/Hosting/GameBootstrapper.cs` 的 `AppBootstrapConfig` 约定，即只包含 `ModPaths`。

## 5 平台特定说明

### 5.1 Linux / Raylib

- `libraylib.so`（raylib 5.5, x64）已签入 `src/Platforms/Desktop/`
- `SkiaSharp.NativeAssets.Linux` NuGet 包提供 `libSkiaSharp.so`
- csproj 使用 OS 条件 `<ItemGroup>` 按平台复制正确的原生库
- 系统依赖: `libx11-dev`, `libxrandr-dev`, `libxi-dev`, `libxcursor-dev`, `libxinerama-dev`, `libgl1-mesa-dev`

### 5.2 Cloud VM 限制

- Raylib 桌面应用需要 GPU 和原生库，在无 GPU 的 Cloud VM 上可能无法运行
- CLI、bridge、web launcher、editor 技术栈在 Cloud VM 上可用

### 5.3 已知问题

- `src/Tools/Ludots.Editor.React/src/App.tsx` 存在大小写敏感的 import（`@/Components/...` vs `@/components/...`），Linux 上可能导致 `tsc` 和 Vite 失败
- ESLint 仍有历史遗留告警，和 launcher 重构无关

## 6 Mod 路径与工作区约定

仓库内默认扫描根仍然是 `mods/`，但它不再是唯一合法位置。

支持三种发现方式:

- `launcher.config.json` 的 `scanRoots`
- `.\scripts\run-mod-launcher.cmd cli workspace add --path <dir>`
- `.\scripts\run-mod-launcher.cmd cli binding set <name> --path <mod-root>`

因此:

- mod 可以位于仓库外任意目录
- binding 可以把全局变量名稳定映射到任意路径
- 递归扫描仍然由 launcher backend 统一处理

## 7 相关文档

- [编码标准](00_coding_standards.md)
- [Launcher CLI Runbook](../reference/cli_runbook.md)
- [启动顺序与入口点](../architecture/startup_entrypoints.md)
- [Mod 架构与配置系统](../architecture/mod_architecture.md)
