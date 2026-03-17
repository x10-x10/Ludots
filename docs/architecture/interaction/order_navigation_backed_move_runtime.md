# Order Navigation-Backed Move Runtime

> SSOT scope: `moveTo` order 如何在现有 GAS order runtime 内转交给 `Navigation2D` 执行。  
> 本文只覆盖已落地的导航接管移动运行时，不覆盖 queued path 可视化、hover marker、或更高层的右键交互面板。

## 1. 当前缺口

在本切片之前，仓库已经具备以下可复用能力：

- `src/Core/Gameplay/GAS/Systems/MoveToWorldCmOrderSystem.cs`
  - 负责消费 `moveTo` order。
- `src/Core/Navigation2D/Components/NavGoal2D.cs`
  - 负责表达导航目标。
- `src/Core/Ludots.Physics2D/Systems/Navigation2DSimulationSystem2D.cs`
  - 负责基于 `NavGoal2D` 驱动导航代理。
- `src/Core/Ludots.Physics2D/Systems/IntegrationSystem2D.cs`
  - 负责把速度积分回 `Position2D`。
- `src/Core/Ludots.Physics2D/Systems/Physics2DToWorldPositionSyncSystem.cs`
  - 负责把 `Position2D` 同步回 `WorldPositionCm`。

但这些能力在 `Navigation2D.Enabled=true` 的主干运行时里还没有被完整装配到 order 驱动的单位上：

- `GameEngine` 只反射注册了 `Navigation2DSimulationSystem2D`
- commandable 单位默认没有 `Position2D` / `Velocity2D` / `NavAgent2D` / `NavKinematics2D`
- `MoveToWorldCmOrderSystem` 仍然直接 step `WorldPositionCm`

结果是：即使地图打开了 `Navigation2D`，移动 order 也没有真正复用导航运行时。

## 2. 复用清单

按照 `docs/conventions/02_ai_assisted_development.md` §4，本切片显式复用：

- Runtime pipeline: `GameEngine` phase registration
  - 把导航、积分、位置回写仍然挂在现有 `SystemGroup` 上，而不是新造一条移动循环。
- Order runtime: `MoveToWorldCmOrderSystem`
  - 保持 `moveTo` 的唯一 authoritative 消费点不变。
- Navigation runtime: `NavGoal2D` / `NavAgent2D` / `NavKinematics2D`
  - 继续作为导航移动的唯一目标与运动学表达。
- Physics sync: `IntegrationSystem2D` / `Physics2DToWorldPositionSyncSystem`
  - 复用现有 2D 物理同步链，不再手写第二套 world-position 推进逻辑。
- Attribute source: `AttributeBuffer` + `AttributeRegistry`
  - 继续由 `MoveSpeed` 属性驱动移动速度，而不是在导航层再抄一份速度配置。

## 3. 设计原则

### 3.1 `moveTo` 仍然是 order system 的职责

本切片没有把移动 authority 挪到输入层、UI 层、或 mod 层。

`MoveToWorldCmOrderSystem` 仍然负责：

- 读取 active `moveTo` order
- 解析世界坐标目标
- 判断是否到达
- 决定是否完成 order

变化只在于：当实体已具备导航组件时，它不再直接修改 `WorldPositionCm`，而是改为写入 `NavGoal2D`。

### 3.2 导航组件补齐是 Core 级装配，不是沙箱模板 hack

`src/Core/Navigation2D/Systems/NavOrderAgentBootstrapSystem.cs` 负责为具备：

- `OrderBuffer`
- `WorldPositionCm`

的 commandable 单位补齐最小导航/物理组件：

- `Position2D`
- `Velocity2D`
- `Mass2D`
- `NavAgent2D`
- `NavKinematics2D`

这样 champion sandbox 不需要为了演示移动，再在每个英雄模板里手工重复铺一套导航组件。

### 3.3 `WorldPositionCm` 仍然是对外统一世界坐标

导航运行时内部移动使用 `Position2D`，但外部 order / selection / presentation 继续读 `WorldPositionCm`。

因此必须复用 `Physics2DToWorldPositionSyncSystem`，而不是让各消费方开始直接依赖 `Position2D`。

## 4. Runtime 结构

当 `Navigation2D.Enabled=true` 时，`src/Core/Engine/GameEngine.cs` 现在会注册：

1. `src/Core/Navigation2D/Systems/NavOrderAgentBootstrapSystem.cs`
   - 放在 `SystemGroup.InputCollection`
   - 补齐 commandable 单位的最小导航/物理组件，并对齐 `Position2D <- WorldPositionCm`
2. `src/Core/Ludots.Physics2D/Systems/Navigation2DSimulationSystem2D.cs`
   - 继续负责基于 `NavGoal2D` 计算速度
3. `src/Core/Ludots.Physics2D/Systems/IntegrationSystem2D.cs`
   - 继续负责 `Velocity2D -> Position2D`
4. `src/Core/Ludots.Physics2D/Systems/Physics2DToWorldPositionSyncSystem.cs`
   - 放在 `SystemGroup.PostMovement`
   - 负责 `Position2D -> WorldPositionCm`

`src/Core/Gameplay/GAS/Systems/MoveToWorldCmOrderSystem.cs` 的行为变为：

- 有导航组件时：
  - 写 `NavGoal2D.Point`
  - 到达停距后清空 goal 并完成 order
- 没有导航组件时：
  - 仍使用当前直接 step 逻辑

这个双路径只用于衔接当前仓库里尚未统一完成导航装配的世界；一旦所有目标场景稳定切到 Navigation2D，应继续收敛非导航路径。

## 5. 代码锚点

- `src/Core/Engine/GameEngine.cs`
- `src/Core/Navigation2D/Systems/NavOrderAgentBootstrapSystem.cs`
- `src/Core/Gameplay/GAS/Systems/MoveToWorldCmOrderSystem.cs`
- `src/Core/Ludots.Physics2D/Systems/Navigation2DSimulationSystem2D.cs`
- `src/Core/Ludots.Physics2D/Systems/IntegrationSystem2D.cs`
- `src/Core/Ludots.Physics2D/Systems/Physics2DToWorldPositionSyncSystem.cs`
- `mods/showcases/champion_skill_sandbox/ChampionSkillSandboxMod/assets/Configs/Navigation2D/navigation2d.json`

## 6. 测试证据

- `src/Tests/GasTests/OrderNavigationMoveRuntimeTests.cs`
  - 验证 bootstrap 会从 `WorldPositionCm` 补齐导航/物理组件，并继承 `MoveSpeed`
  - 验证 nav agent 的 `moveTo` order 会写 `NavGoal2D` 而不是直接推进世界坐标
  - 验证进入停距后 order 会完成并清空 `NavGoal2D`
- `src/Tests/GasTests/OrderCompositePlannerTests.cs`
  - 继续验证上一切片的复合命令规划未被导航接管改坏

## 7. 非目标

以下内容不属于本切片：

- 右键移动路径显示
- queued move / cast path indicator
- hover target marker
- shift 多段指令 UI
- 范围外施法后的 runtime replanning

这些能力必须建立在本切片已经提供的统一导航移动执行链上，再继续往 indicator performer 和 command panel 收敛。
