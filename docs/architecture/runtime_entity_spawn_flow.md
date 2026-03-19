# 运行时实体生成链路

本文定义 Ludots 当前的运行时实体生成标准流程，并明确它与地图初始化实体创建的职责边界。

核心结论：

* 地图初始化实体创建走 `MapLoader.LoadEntities(mapConfig)`。
* 运行时 gameplay 实体创建统一走 `RuntimeEntitySpawnRequest -> RuntimeEntitySpawnQueue -> RuntimeEntitySpawnSystem`。
* `CreateUnit`、`CreateProjectile`、运行时法阵、召唤物、墙体、柱子、临时 blocker 等，都必须复用这条链路。
* 缺队列、队列满、未知类型时直接失败，不做 fallback。

## 1 为什么分成两条链路

Ludots 有两类完全不同的实体创建时机：

* 地图初始化：随着地图加载一次性实例化静态内容和初始单位。
* 运行时生成：由 Effect、Input、Trigger、脚本或 Mod 在游戏运行中按事件请求生成。

这两类行为不能混在一条路径里。前者依赖地图与 session 生命周期，后者依赖固定步运行时上下文与统一 phase 落地时机。

## 2 地图初始化链路

地图加载时，引擎顺序是：

```text
GameEngine.LoadMap(mapId)
  -> CreateBoardsForSession(session, mapConfig)
  -> ApplyBoardSpatialConfig(primaryBoard)
  -> MapLoader.LoadEntities(mapConfig)
```

相关代码：

* `src/Core/Engine/GameEngine.cs`
* `src/Core/Engine/MapLoader.cs`

这条链路不是运行时 spawn 接口，运行时逻辑不得调用它来偷创建实体。

## 3 标准运行时生成链路

运行时实体统一使用 `RuntimeEntitySpawnRequest`：

```text
Producer
  -> build RuntimeEntitySpawnRequest
  -> RuntimeEntitySpawnQueue.TryEnqueue(request)
  -> RuntimeEntitySpawnSystem.Update()
  -> materialize entity by Kind
  -> optional OnSpawn effect
```

当前支持三种来源：

* `RuntimeEntitySpawnKind.UnitType`
* `RuntimeEntitySpawnKind.Template`
* `RuntimeEntitySpawnKind.Assembly`

`RuntimeEntitySpawnSystem` 的职责只有三件事：

* 校验请求是否合法
* 物化实体并补齐必要运行时组件
* 传播 team / map / owner / parent relation，并在需要时发布 `OnSpawn`

## 4 `CreateUnit` 与 `CreateProjectile` 的职责

`CreateUnit` 与 `CreateProjectile` 都是 GAS builtin handler，但它们都不直接创建实体。

它们只负责：

* 从 `EffectContext` 解析生成原点与初始参数
* 组装 `RuntimeEntitySpawnRequest`
* 把请求写入 `RuntimeEntitySpawnQueue`

这意味着：

* `Projectile` 不再是绕过标准 spawn contract 的特例
* summon 的 owner / parent 传播也开始沉到统一 spawn 基建
* 运行时生成细节不允许重新塞回 builtin handler 或 Mod

## 5 当前已落地的两刀

### 5.1 第一刀：projectile / summon 统一收口

当前实现已经完成：

* `CreateProjectile` 改为入队 `RuntimeEntitySpawnKind.Assembly`
* `RuntimeEntitySpawnRequest` 支持 `ProjectileState`
* `RuntimeEntitySpawnRequest` 支持 `CopySourcePlayerOwner`
* `RuntimeEntitySpawnRequest` 支持 `LinkSourceAsParent`

因此 `ProjectileState` 只是行为组件，summon 也只是统一 spawn contract 的某种物化结果。

### 5.2 第二刀：阻挡型具现体下沉到 physics / nav

wall、pillar、arena blocker、temporary terrain 等不再需要新的 gameplay runtime 大类。

当前做法是：

* Core authoring 用 `ManifestationObstacleIntent2D`、`ManifestationObstaclePolygon2D` 表达阻挡意图
* `Ludots.Physics2D` 中的 `ManifestationObstacleBridge2DSystem` 把意图下沉为 `Collider2D`、`NavObstacle2D`、`NavKinematics2D`
* `NavObstacle2D` 明确区分 `Circle / Box / Polygon`

职责边界：

* “这是不是墙、柱子、法阵”属于 gameplay recipe 语义
* “它如何参与碰撞与寻路阻挡”属于低层 physics / nav 语义

## 6 可插拔空间模块约束

当前 blocker bridge 只在 `Ludots.Physics2D` 中实现连续空间版本，这是刻意的模块边界，而不是缺实现。

约束如下：

* Core authoring 不依赖 continuous-space 细节
* 引擎只按模块可用性注册 bridge system
* `Hex / Grid` 后端未来若要支持 blocker manifestation，应提供各自 sink system
* 不允许在 GAS handler、effect config 或 authoring 层写 `if hex / if grid / if continuous`

## 7 验证证据

本次链路已有自动化验证：

* `src/Tests/GasTests/TagEffectArchitectureTests.cs`
* `src/Tests/GasTests/RuntimeManifestationBridgeTests.cs`
* `src/Tests/Navigation2DTests/Navigation2DFlowCrowdFieldTests.cs`
* `src/Tests/GasTests/Production/ChampionSkillSandboxPlayableAcceptanceTests.cs`

验收产物见：

* `artifacts/acceptance/champion-skill-sandbox/battle-report.md`
* `artifacts/acceptance/champion-skill-sandbox/trace.jsonl`
* `artifacts/acceptance/champion-skill-sandbox/path.mmd`

## 8 2026-03 Runtime extensions for unified manifestations

Recent MOBA showcase work extended the same spawn contract instead of adding a
second projectile or beam runtime:

* `CreateUnit` now supports `placementPattern` / `facingPattern` so circle
  formations such as Jarvan-style arenas can be authored as one effect config.
  The current reusable patterns cover circle placement plus radial / tangent
  facing.
* `ManifestationMotion2D` keeps spawned manifestations attached to the parent
  entity or parent execution aim, including optional `forwardOffsetCm`. This is
  the lower-layer primitive used by steerable lasers and other attached line
  manifestations.
* `DestroyWhenParentExecutionEnds` ties temporary manifestations to the parent
  `AbilityExecInstance` lifetime. This is how channel beams, temporary arena
  walls, and other execution-scoped manifestations clean themselves up without
  bespoke gameplay stacks.
* `AbilityExecAimSync` keeps the active execution target/facing in sync with the
  authoritative input context while an execution is still alive. This gives the
  runtime a continuous aim feed for channels without reading render-frame input
  directly inside gameplay systems.

Design boundary:

* Core authoring still describes intent only: summon, zone, blocker, beam, or
  other manifestation behavior is expressed via components, tags, effect
  presets, and performer bindings.
* Physics / navigation remain the sink owners for blocker materialization. Wall,
  pillar, and arena segments still flow through `ManifestationObstacleIntent2D`
  and backend-specific bridge systems.
* Spatial backend differences stay outside GAS authoring. Continuous-space
  support currently ships with `Ludots.Physics2D`; future `Hex` / `Grid`
  backends should add their own sink systems instead of branching in effect
  handlers or config.

## 9 2026-03 Ability-level held input contract

The unified manifestation runtime now also treats sustained beam / channel input
as a reusable GAS-order concern instead of hero-specific control code.

Additions:

* `AbilityDefinition.input` can override the selected slot's local input
  contract with `trigger`, `heldPolicy`, and `castModeOverride`.
* `InputOrderMappingSystem` resolves that override from the effective ability
  slot after form / granted-slot routing, so QWER bindings stay generic while
  individual abilities can opt into `Held + StartEnd`.
* Held lifecycle now sinks into formal order types: `castAbility.Start`
  activates the exec, `castAbility.End` ends the matching slot exec through
  `AbilityEndOrderSystem`.
* `StopOrderSystem` remains the global stop / cancel path. Channel end is no
  longer modeled as "pretend this was a stop".

Resulting boundary:

* Authoring can express "hold to channel, release to end" without hardcoding a
  hero, slot, performer, or manifestation subtype.
* Presentation panels resolve hint text against the effective ability-level cast
  mode, so UI copy stays aligned with the actual runtime contract.
