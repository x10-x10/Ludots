# Order Navigation Pathing Bootstrap

> SSOT scope: order/move 后续切片要消费的 map-scoped pathing runtime 装配方案。
> 本文只覆盖 `PathStore` / `IPathService` 如何进入 `GameEngine` 与地图生命周期，不把 move execution、order chaining、indicator performer 提前写成已落地能力。

## 1. 现状缺口

当前仓库已经具备 pathing 的核心拼图，但还没有正式接到地图切换主线上：

- `src/Core/Navigation/Pathing/PathStore.cs`
- `src/Core/Navigation/Pathing/IPathService.cs`
- `src/Core/Navigation/Pathing/AutoPathService.cs`
- `src/Core/Navigation/Pathing/PathServiceRouter.cs`
- `src/Core/Navigation/Pathing/Config/PathingConfigLoader.cs`
- `assets/Configs/Navigation/pathing.json`

与此同时，地图切换已经会刷新 navmesh 查询服务，但只停留在 navmesh 层：

- `src/Core/Engine/GameEngine.cs`
- `src/Core/Navigation/NavMesh/NavQueryServiceRegistry.cs`

这意味着后续切片虽然可以复用现成 pathing 类型，却没有稳定的 engine service 入口可消费，order orchestration 只能继续绕回临时逻辑。

## 2. 复用清单

按 `docs/conventions/02_ai_assisted_development.md` §4，本任务显式复用以下基建：

- Registry: `NavMeshProfileRegistry`
  - 用于把 `assets/Configs/Navigation/pathing.json` 中的 `profileId` 编译到运行时 profile index。
- Pipeline: `ConfigPipeline` + `PathingConfigLoader`
  - 用于从统一配置管线加载 pathing agent 配置，而不是额外造一套独立 JSON 读取链路。
- System / Runtime: `GameEngine.LoadMap` / `UnloadMap` / `PushMap` / `PopMap`
  - 用于把 pathing 跟现有 map focus stack 一起热切换。
- Board infrastructure: `MapSession.PrimaryBoard` + `INodeGraphBoard.GraphStore`
  - 用于从当前 board 提取 node graph 视图，而不是建立平行地图缓存。
- Navigation runtime: `NavQueryServiceRegistry`
  - 继续作为 navmesh domain 的唯一查询来源。

## 3. 装配原则

### 3.1 Pathing 是 map-scoped runtime

`PathStore` 和 `IPathService` 都必须跟随当前 focused map 重建，不能做成全局单例。原因：

- path handle 生命周期不能跨 map 复用。
- 当前 board / 当前 navmesh profile store 都是 map-scoped。
- 后续 move preview、auto-move-to-cast、path indicator 都必须读取当前 map 的同一份权威 path runtime。

### 3.2 Pathing bootstrap 不重写输入或移动

这一切片不改以下职责边界：

- 输入仍由 `src/Core/Input/Orders/InputOrderMappingSystem.cs` 负责。
- order queue 仍由 `src/Core/Gameplay/GAS/Orders/OrderQueue.cs` 等现有链路负责。
- move runtime 现阶段仍由现有 `MoveToWorldCmOrderSystem` 执行。
- 路径可视化未来要复用 indicator/performer/overlay 体系，而不是单独造 UI path renderer。

## 4. 目标运行时

地图加载完成后，`GameEngine.GlobalContext` 中应具备以下 map-scoped 服务：

- `CoreServiceKeys.PathingConfig`
- `CoreServiceKeys.PathStore`
- `CoreServiceKeys.PathService`

装配逻辑：

1. 先沿用现有 `LoadNavForMap(...)` 刷新 navmesh services。
2. 再根据当前 `MapSession.PrimaryBoard` 生成 pathing graph 视图：
   - `INodeGraphBoard`：使用 `GraphStore.BuildLoadedView()`
   - 非 node graph board：使用空 graph，占位但不复制地图系统
3. 用统一配置 `assets/Configs/Navigation/pathing.json` 构建 pathing runtime。
4. 若当前 map 已具备 navmesh query services，则优先暴露 `AutoPathService`，并在可行时通过 `PathServiceRouter` 同时接出 direct node-graph / navmesh domain。
5. 当 focused map 为空时，清空这些 map-scoped pathing services，避免保留 stale handle / stale query。

## 5. 与后续切片的衔接

本切片完成后，后续切片直接建立在这些入口上：

- `order-orchestration`
  - 把“移动到施法点再放技能”“超时 order”“shift 多指令队列”编排到 `OrderQueue` 之前。
- `move-runtime-navigation`
  - 用 nav-driven runtime 替换当前直接改 `WorldPositionCm` 的移动执行。
- `order-indicator-and-acceptance`
  - 把 move path / queued order path / hover target feedback 收敛到 indicator performer。

## 6. 非目标

以下内容不属于本切片：

- 右键移动路径渲染
- shift queue UI
- 超出施法距离后的自动走位施法
- hover marker / selection marker 表现层
- projectile performer / cast cue 表现

这些都必须建立在先有稳定 path service 的前提上，再分切片继续推进。
