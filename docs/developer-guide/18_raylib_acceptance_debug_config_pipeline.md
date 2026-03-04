# Raylib 验收调试能力的 ConfigPipeline 配置化

本篇定义 Raylib 侧相机/裁剪/模型可视化验收能力的配置入口，目标是把交互调试参数统一收敛到 `ConfigPipeline`，让 Core 默认值与 Mod 覆写走同一条基建链路。

## 1 配置入口与合并策略

验收调试配置文件路径为 `Presentation/acceptance_debug.json`，在 `config_catalog.json` 中声明为 `DeepObject` 合并策略。

配置来源和优先级如下：

1. `Core:Configs/Presentation/acceptance_debug.json`
2. `<ModId>:assets/Presentation/acceptance_debug.json`
3. `<ModId>:assets/Configs/Presentation/acceptance_debug.json`

参考实现：`assets/Configs/config_catalog.json`、`src/Core/Config/ConfigPipeline.cs`。

## 2 数据结构

配置结构由 `AcceptanceDebugConfig` 定义，核心字段如下：

* `console.toggleKey`：GM 控制台切换键（默认 `F8`）。
* `console.allowCommandsWithoutPrefix`：是否允许省略 `gm` 前缀。
* `accept.defaultFocusDistanceCm`：`accept.focus` 未传距离时的默认值。
* `accept.caseOn`：`accept.case on` 一键启用的渲染调试预设。
* `accept.casePresets[]`：可命名的验收预设，配合 `accept.case <caseId>` 使用。
* `accept.focusPresets[]`：可命名焦点预设，配合 `accept.focuspreset <presetId>` 使用。
* `accept.probeVisual`：验收探针绘制参数（数量、体积、透明度、球头半径）。

参考实现：`src/Core/Presentation/Config/AcceptanceDebugConfig.cs`。

## 3 启动接线

接线顺序如下：

1. `GameEngine` 通过 `AcceptanceDebugConfigLoader` 从 `ConfigPipeline` 读取配置。
2. 加载结果写入 `GlobalContext[ContextKeys.AcceptanceDebugConfig]`。
3. `RaylibHostLoop` 启动时读取该配置，注入 `RaylibGmCommandConsole`。
4. `DrawAcceptanceProbes` 每帧使用 `accept.probeVisual` 参数绘制探针。

参考实现：`src/Core/Engine/GameEngine.cs`、`src/Core/Scripting/ContextKeys.cs`、`src/Adapters/Raylib/Ludots.Adapter.Raylib/RaylibHostLoop.cs`、`src/Core/Presentation/Config/AcceptanceDebugConfigLoader.cs`。

## 4 GM 命令与配置映射

当前命令和配置关系：

* `accept.case on`：应用 `accept.caseOn`。
* `accept.case <caseId>`：应用 `accept.casePresets` 中同名预设。
* `accept.focus <keyword> [distanceCm]`：默认距离来自 `accept.defaultFocusDistanceCm`。
* `accept.focuspreset <presetId>`：从 `accept.focusPresets` 读取 `keyword + distanceCm`。
* `accept.probe on|off`：开关探针渲染，探针样式来自 `accept.probeVisual`。
* 控制台开关按键显示使用 `console.toggleKey`。

参考实现：`src/Adapters/Raylib/Ludots.Adapter.Raylib/Debug/RaylibGmCommandConsole.cs`。

## 5 配置示例

Core 默认配置文件：`assets/Configs/Presentation/acceptance_debug.json`。  
MOBA 覆写示例：`src/Mods/MobaDemoMod/assets/Presentation/acceptance_debug.json`。

这两个文件展示了两类实践：

* 在 Core 中定义通用默认值，保证任何地图都可直接使用验收命令。
* 在 Mod 中覆写 `caseOn`、`casePresets`、`focusPresets`，把项目特定验收流程固化为数据。

## 6 验收执行建议

可按以下顺序执行交互验收：

1. 打开 GM 控制台，执行 `gm accept.case on`。
2. 执行 `gm accept.focuspreset village` 或 `gm accept.focus <keyword>`。
3. 执行 `gm cull.state` 与 `gm accept.inspect` 对照模型绘制/裁剪统计。
4. 若需要专项配置，执行 `gm accept.case <caseId>` 切换预设。

## 7 相关文档

* Config 合并规则：见 [07_config_pipeline.md](07_config_pipeline.md)
* 外观资产链路：见 [17_entity_visual_asset_pipeline.md](17_entity_visual_asset_pipeline.md)
* 适配器边界：见 [03_adapter_pattern.md](03_adapter_pattern.md)
