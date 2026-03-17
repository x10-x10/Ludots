# RFC-0058 运行时具现体与空间查询策略统一合同

本 RFC 提议把 `projectile`、`summon`、`beam`、`ray`、`persistent zone`、`wall`、`trap`、`spline missile` 等 MOBA 常见法术形态，统一收敛为一套组合式的“运行时具现体”合同，而不是继续围绕 `Projectile` 这个单一名词加字段、加专用系统。

这里选择“具现体”而不是“空间派生体”，原因有两点：

* 某些 runtime object 天然并不等于某一种空间形状，例如 summon ownership、beam tether、控制权传播，先是 gameplay runtime object，之后才决定是否参与空间层。
* “派生体”容易让人联想到 OOP 类型树，而 Ludots 需要的是 recipe + component + effect 的组合式建模。

目标是在不引入平行玩法栈的前提下，让 Ludots 覆盖 MOBA 中几乎所有法术与召唤物场景，并保持对 `Hex / Grid / Continuous` 空间实现的可插拔兼容。

## 1 问题

当前仓库已经具备三条重要基线：

* 空间查询已经按“形状能力”建模，而不是按“技能种类”建模，见 `src/Core/Spatial/ISpatialQueryService.cs`。
* GAS 已经把“查询 -> 过滤 -> payload effect 派发”拆成多层描述符，见 `src/Core/Gameplay/GAS/EffectTemplateRegistry.cs`。
* `CreateUnit` 已经走统一的运行时生成链路，见 `src/Core/Gameplay/Spawning/RuntimeEntitySpawnQueue.cs` 与 `src/Core/Gameplay/Spawning/RuntimeEntitySpawnSystem.cs`。

真正的问题在于：

1. `Projectile` 历史上被当成了 runtime 大类，而不是某种行为 recipe。
2. 召唤物、法阵、墙体、柱子、持续 beam、spline 飞弹，会因为命名差异被拆成多套实现。
3. 物理碰撞、导航阻挡、owner/parent 传播、命中策略、生命周期策略，这些本该分层的维度容易重新缠回 feature 语义里。

## 2 目标

本 RFC 需要满足以下目标：

1. 用一套 Core 合同覆盖 MOBA 常见 runtime spell scenes：
   * 线性飞弹
   * 穿透 / 反弹 / 回旋 / 追踪 / 分裂飞弹
   * 持续 beam / ray / tether
   * 圆形 / 矩形 / 扇形 / 环形法阵
   * wall / pillar / blocker zone / trap
   * summon / turret / pet / controllable spell body
   * spline / path based projectile
2. 继续以 `Effect + Tag + Graph + TargetResolver` 作为玩法表达主线，不把技能行为重新硬编码回 runtime 系统。
3. 让 `Hex / Grid / Continuous` 的差异落在空间策略与低层 sink，而不是扩散到 builtin handler、effect authoring 或 feature system。
4. 只有在“跨 tick 持有运行时状态”确实不可避免时，才允许引入新的 runtime system。

## 3 非目标

本 RFC 不做以下事情：

* 不强制所有法术都生成 entity。
* 不引入 `ProjectileSystem / BeamSystem / ZoneSystem / TrapSystem / SummonSystem` 这种按玩法名词拆分的平行 runtime 栈。
* 不在 Core authoring 中写 `if hex / if grid / if continuous` 的后端分支。

## 4 设计原则

### 4.1 先判断是否需要跨 tick 状态，再决定是否生成 entity

以下形态通常不需要生成运行时实体：

* 一次性直线伤害
* 一次性圆形 / 矩形 / 扇形 / ring 结算
* 一次性 raycast / line query

这类能力继续走：

```text
EffectRequest
  -> TargetResolver(Query + Filter + Dispatch)
  -> Payload Effect
```

以下形态通常需要运行时实体：

* 有持续时间
* 会移动
* 需要命中计数
* 命中后仍继续存在
* 会追踪 / 反射 / 回旋 / 分裂
* 可被控制或可被阻挡
* 自身会作为空间存在物被别的系统查询
* 需要继承 owner / player control / parent relation

### 4.2 Core 不拥有“Projectile 思维”，只拥有“具现体行为原语”

`Projectile` 可以继续作为 authoring 名词存在，但 Core 不应把它当成 runtime 顶层分类。Core 真正应持有的是：

* 生成合同
* ownership / relation 传播
* 生命周期策略
* 运动策略
* 空间采样策略
* 命中策略
* effect 发射钩子
* presentation identity

### 4.3 召唤物不是另一条特例层

在 Ludots 里，summon 不是介于 projectile 与 unit 之间的神秘类别，而是运行时实体生成的一个结果：

* 可继承 `Team`
* 可继承 `PlayerOwner`
* 可建立 `ChildOf` / `ChildrenBuffer`
* 是否可控、是否 AI 驱动、是否周期发射 effect，都由模板组件与 tag/effect 组合决定

### 4.4 空间差异由查询策略层和后端 sink 吸收

Gameplay authoring 只表达“我想查询或成为怎样的空间语义”，例如：

* 从 source 发出 line / cone / circle 查询
* 沿 velocity 做 path sweep
* 定期在 ring 内采样
* 把自己声明为 blocker manifestation

Hex / Grid / Continuous 的差异应由空间层解释，而不是落成：

```text
if hex -> ...
else if grid -> ...
else if continuous -> ...
```

## 5 统一模型

### 5.1 顶层分类

统一后，法术形态分三类：

1. 瞬时空间结算
   * 不生成 entity
   * 直接走 `TargetResolver + SpatialQueryStrategy`
2. 运行时具现体
   * 生成 runtime entity
   * 跨 tick 持有状态
   * 由统一 spawn contract 物化
3. 表现具现
   * 只有视觉表达，没有 gameplay 权威状态
   * 继续由 performer / presentation pipeline 负责

### 5.2 具现体 recipe 不是类型树，而是组件组合

一个运行时具现体通常由以下维度组合而成：

* Identity / Ownership
* Lifetime
* Motion
* Sampling
* Hit Policy
* Effect Emission
* Optional Blocker Intent

也就是说，projectile、summon、zone、wall 只是不同 recipe，不是不同 runtime 基建。

## 6 运行时生成合同

### 6.1 SSOT 仍然是 `RuntimeEntitySpawnRequest`

统一后的低层合同仍然是：

```text
RuntimeEntitySpawnRequest
  -> RuntimeEntitySpawnQueue
  -> RuntimeEntitySpawnSystem
```

当前 request 支持三种来源：

* `UnitType`
* `Template`
* `Assembly`

### 6.2 第一刀：projectile / summon 收口到统一 spawn

当前 worktree 已经完成：

* `CreateProjectile` 不再直接 `world.Create(...)`，而是入 `RuntimeEntitySpawnQueue`
* `RuntimeEntitySpawnKind` 新增 `Assembly`
* `RuntimeEntitySpawnRequest` 允许携带 `ProjectileState`
* spawn request 支持 `CopySourcePlayerOwner`
* spawn request 支持 `LinkSourceAsParent`

因此：

* `Projectile` 不再享有绕过标准 spawn contract 的特权
* summon 的 owner / parent 传播开始下沉到 spawn 基建
* `ProjectileRuntimeSystem` 只是消费 `ProjectileState` 的行为系统，而不是法术 runtime 总入口

### 6.3 第二刀：阻挡型具现体下沉为低层桥接

这轮 worktree 进一步解决 wall / pillar / arena / blocker zone 这类“会作为空间障碍存在”的具现体。

当前实现选择：

* Core 只声明 `ManifestationObstacleIntent2D`、`ManifestationObstaclePolygon2D`
* 连续空间下沉放在 `src/Core/Ludots.Physics2D/Systems/ManifestationObstacleBridge2DSystem.cs`
* bridge 把 blocker intent 物化为 `Collider2D`、`Mass2D.Static`、`Velocity2D.Zero`、`NavObstacle2D`、`NavKinematics2D`
* `NavObstacle2D` 扩展为 shape-aware runtime：`Circle / Box / Polygon`

这背后的职责边界是：

* wall / Ornn pillar / Anivia 墙 / Jarvan R arena 的高层身份仍然只是 runtime manifestation recipe
* 真正的碰撞、静态质量、导航阻挡、flow obstacle stamping 属于 physics / nav 层职责
* 因此不新增 `WallSystem`、`ArenaSystem`、`PillarSystem` 这种 feature 名词系统

## 7 空间查询策略与后端可插拔约束

### 7.1 authoring 视角

为了让具现体和瞬时技能都能复用同一套空间表达，authoring 视角至少应包含：

* `Metric`
* `Shape`
* `OriginMode`
* `DirectionMode`
* `SamplingMode`
* `BoardResolutionMode`

Gameplay authoring 配的是“查询意图”或“阻挡意图”，不是“Hex 怎么查 / Grid 怎么查 / Continuous 怎么查”。

### 7.2 只有底层能力缺失时才补系统

只有以下能力无法由现有查询服务表达时，才值得补新的底层服务：

* ordered raycast first-hit
* path / spline sweep
* surface normal retrieval
* ordered contact stream

它们属于空间层，不属于 projectile 层。

### 7.3 可插拔约束

这轮只实现了 continuous-space 分支的 blocker sink，但 authoring 合同故意没有绑定某一种空间实现。

约束如下：

* gameplay authoring 只写 blocker intent，不写后端分支逻辑
* `Ludots.Physics2D` 通过 bridge 消费 blocker intent，并下沉到连续空间 collider / nav obstacle
* `Hex / Grid` 后端如果未来需要 blocker manifestation，应各自提供自己的 sink system
* 引擎注册层只负责发现并挂接某个空间模块提供的 sink，不把具体 shape 解释逻辑带回 Core

## 8 MOBA 场景映射

| 场景 | 是否需要 entity | 建议合同 |
|------|----------------|---------|
| 一次性 Lux R / laser | 否 | `TargetResolver + QueryLine / Raycast` |
| 一次性扇形 / 矩形 / circle | 否 | `TargetResolver + SpatialQueryStrategy` |
| Ezreal Q / 线性飞弹 | 是 | `Assembly + ProjectileState + line sampling` |
| 穿透飞弹 | 是 | `Assembly + projectile behavior + pierce policy` |
| 回旋飞弹 | 是 | `Assembly + projectile behavior + return policy` |
| 追踪飞弹 | 是 | `Assembly + homing motion` |
| Spline 飞弹 | 是 | `Assembly + path motion + path sweep` |
| 持续 beam | 视是否跨 tick 独立状态而定 | channel effect 或 runtime manifestation |
| 持续法阵 / 毒云 / healing zone | 是 | `Template/UnitType + periodic search` |
| Trap / wall / temporary terrain | 是 | `Template/UnitType/Assembly + manifestation obstacle intent + backend sink` |
| Summon / turret / pet | 是 | `UnitType/Template + owner propagation + optional parent link` |
| 可操控法术体 | 是 | `Assembly/Template + PlayerOwner + input-driven motion` |
| Ornn 柱子 / Anivia 墙 / 临时障碍柱 | 是 | `runtime manifestation + blocker intent + physics/nav sink` |
| Jarvan R 场地边界 | 是 | `multiple blocker manifestations` 或上层组合式 arena pattern |

## 9 当前落地状态

本 worktree 已经具备以下代码与验证：

* `CreateProjectile` 与 `CreateUnit` 统一走 `RuntimeEntitySpawnQueue`
* `ProjectileState` 下沉为普通 runtime behavior component
* `ManifestationObstacleIntent2D` 作为 Core authoring 合同存在
* `ManifestationObstacleBridge2DSystem` 在 `Ludots.Physics2D` 中完成连续空间 blocker sink
* `CrowdSurface2D` 与 `Navigation2DSteeringSystem2D` 已支持 box / polygon obstacle stamping
* 测试覆盖了 spawn 合同、blocker bridge、polygon authoring、导航阻挡 shape 行为

## 10 收敛建议

1. 后续凡是新增 runtime 法术形态，优先判断它是不是新 recipe，而不是先发明新 runtime 大类。
2. 需要空间阻挡时，先声明 blocker intent，再看是否要由某个空间后端提供 sink，不要把碰撞/nav 语义硬塞回 GAS handler。
3. Jarvan R 这类 arena 技能优先作为“多个 blocker manifestation 的组合”设计，而不是在 Core 里加入 arena 专有概念。
4. 如果未来 Hex / Grid 后端支持 blocker manifestation，应复用同一 authoring 合同并提供各自 sink system，而不是改写 effect authoring。
