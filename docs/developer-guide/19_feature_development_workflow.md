# Feature 开发工作流规范

本篇定义 Ludots 仓库中新功能开发的完整流程，从需求发现到合并验收。核心目标：杜绝重复造轮子，消除幻觉代码（引用不存在的 API），确保每一行新代码都挂靠在已有架构管线上。

## 1 适用范围

本规范适用于以下场景：

*   新增 Core 层 System、Component、Registry
*   新增或修改 Mod（包括 gameplay 逻辑、配置、资源）
*   新增 Adapter 层实现（Raylib、Editor 等）
*   AI Agent（Cursor、Claude 等）辅助开发的全部场景

简单的 bugfix、文档修订、配置调整不强制执行全部流程，但仍需遵守第 5 节的验证清单。

## 2 发现阶段——动手之前必须做的事

发现阶段的目标是 **确认要做的事不是已有能力的重复**。跳过这一步是重复造轮子的首要原因。

### 2.1 已有能力检索清单

在写任何代码之前，按以下清单逐项检索：

| 检索对象 | 检索方法 | 典型位置 |
|---------|---------|---------|
| 已有 System | 搜索 `BaseSystem<World` 和 `SystemGroup` | `src/Core/` 各子目录 |
| 已有 Registry | 搜索 `Registry` 类名 | 见本文第 7 节速查表 |
| 已有 Component | 搜索目标语义的 `Cm`/`Tag`/`Event` 后缀组件 | `src/Core/Components/`、各子模块 |
| 已有 Mod | 检查 `mods/` 目录下所有 `mod.json` | `mods/*/mod.json` |
| 已有配置管线 | 搜索 `ConfigCatalogEntry`、`MergeGameConfig` | `src/Core/Config/` |
| 已有 Sink | 搜索 `AttributeSinkRegistry` 注册点 | `src/Core/Gameplay/GAS/Bindings/` |
| 已有 Trigger/Event | 搜索 `EventKey`、`OnEvent` | `src/Core/Scripting/` |
| 已有文档 | 浏览 `docs/developer-guide/README.md` 目录 | `docs/developer-guide/` |

### 2.2 发现结论记录

检索完成后，用一句话记录发现结论，格式如下：

```
发现：[已有/无] 可复用能力。
- 可复用：<列出具体类名、Registry、System>
- 需新增：<列出确认不存在、需要从零实现的部分>
- 需扩展：<列出已有但需修改接口的部分>
```

如果发现结论为"全部需新增"，需要额外确认：是否遗漏了检索项？是否搜索了正确的关键词？

### 2.3 AI Agent 发现规则

AI Agent 在发现阶段必须执行以下操作：

1. **搜索而非猜测**：对任何要引用的类、方法、接口，先用搜索工具确认其存在及签名，不得凭记忆或推测编写调用代码
2. **读文档再动手**：先读 `docs/developer-guide/README.md` 定位相关架构文档，通读后再设计方案
3. **列出复用清单**：在开始编码前，显式列出计划复用的已有类和将要新建的类

## 3 设计阶段——写代码之前先写方案

### 3.1 设计方案内容

对于非 trivial 的功能（涉及 2 个以上文件的修改），在编码前产出简要设计方案。方案不需要长篇大论，但必须包含：

| 项目 | 内容 | 目的 |
|-----|------|-----|
| 目标 | 一句话说明这个功能做什么 | 对齐理解 |
| 挂靠点 | 新代码将注册到哪个 Registry、属于哪个 SystemGroup phase、挂靠哪条管线 | 防止游离代码 |
| 复用清单 | 列出将使用的已有类、接口、Registry（附源码路径） | 防止重复造轮子 |
| 新增清单 | 列出需要新建的类、组件、配置（附计划路径） | 明确增量 |
| 数据流 | 新功能的输入从哪来、输出到哪去，经过哪些 phase | 确保架构一致 |
| 测试策略 | 如何验证功能正确性（单元测试 / 集成测试 / 手动验证） | 防止提交未验证代码 |

### 3.2 API 引用验证（防幻觉条款）

设计方案中引用的每一个 API 必须通过以下验证：

*   **类存在性**：搜索确认类/接口定义存在于代码库中
*   **方法签名**：确认方法名、参数类型、返回类型与实际代码一致
*   **注册模式**：确认 Registry 的 `Register` 方法签名和调用时机（schema phase / runtime）
*   **组件布局**：确认 ECS 组件是 blittable struct，字段类型正确

违反此条的代码一律不得提交。AI Agent 生成的代码尤其需要逐项验证。

### 3.3 设计方案的 Review

*   如果是人类开发者：在 Issue 或 PR description 中记录设计方案，获得至少一位成员确认
*   如果是 AI Agent：在开始编码前将方案输出给用户确认（除非用户明确授权自主执行）

## 4 实现阶段——编码规范

### 4.1 架构挂靠原则

所有新代码必须挂靠到已有架构管线，不得创建平行体系：

| 要做的事 | 正确做法 | 错误做法 |
|---------|---------|---------|
| 新增 gameplay 属性 | `AttributeRegistry.Register` | 自建 dictionary 存属性 |
| 新增系统 | `GameEngine.RegisterSystem(sys, group)` 或通过 `SystemFactoryRegistry` | 在 Update 里手动调用 |
| 跨层数据传递 | 通过 Sink（`AttributeSinkRegistry`） | 直接写组件跨 phase |
| 配置加载 | 接入 `ConfigPipeline` + `ConfigCatalogEntry` | 自建 JSON 加载器 |
| 事件通信 | `GameplayEventBus` 或 `TriggerManager.OnEvent` | 自建事件系统 |
| 表现更新 | 通过 Performer 管线和 `ResponseChain` | 在 Core 系统里直接调平台 API |
| Mod 入口 | `IMod.OnLoad(IModContext)` | 静态构造器或反射扫描 |

### 4.2 命名与文件位置

| 类型 | 命名规则 | 文件位置 |
|-----|---------|---------|
| 数据组件 | `XxxCm`（如 `HealthCm`） | 所属子模块的 Components 目录 |
| 标签组件 | `XxxTag`（如 `IsDeadTag`） | 同上 |
| 事件组件 | `XxxEvent`（如 `DamageEvent`） | 同上 |
| 系统 | `XxxSystem`（如 `DamageCalculationSystem`） | 所属子模块的 Systems 目录 |
| Registry | `XxxRegistry` | 所属子模块根目录或 Registry 子目录 |
| Mod | `XxxMod`（PascalCase） | `mods/XxxMod/` |

命名禁止耦合具体业务（如不要叫 `MobaHealthSystem`，应叫 `HealthSystem`）。业务差异通过配置驱动，不通过类名区分。

### 4.3 ECS 硬性约束

以下是不可违反的 ECS 规则，违反任意一条视为 blocking issue：

1. 组件必须是 **blittable struct**——不得包含 `string`、`class`、`List<T>` 等引用类型
2. 核心循环 **零 GC**——System 的 `Update` 方法内不得产生托管堆分配
3. `QueryDescription` **缓存为 `private readonly` 字段**——不得在热循环内创建
4. 结构变更使用 `CommandBuffer`——不得在 query 循环内直接 Create/Destroy/Add/Remove
5. gameplay 状态使用 `Fix64`/`Fix64Vec2`——不得使用 `float`
6. 不得依赖 `Dictionary` 迭代顺序、`System.Random`、或其他不确定性来源

### 4.4 Commit 规范

每个 commit 应是一个完整的逻辑单元，遵循以下格式：

```
<type>(<scope>): <description>

[可选 body]
```

| type | 用途 |
|------|------|
| `feat` | 新功能 |
| `fix` | Bug 修复 |
| `refactor` | 重构（不改变外部行为） |
| `docs` | 文档变更 |
| `test` | 测试变更 |
| `chore` | 构建、工具链变更 |

scope 使用模块名（如 `gas`、`physics2d`、`editor`、`mod/MobaDemoMod`）。

## 5 验证阶段——提交前必须完成

### 5.1 验证清单

每次提交前逐项确认：

*   [ ] **编译通过**：`dotnet build` 无 error
*   [ ] **测试通过**：相关测试项目全量通过
*   [ ] **无重复实现**：grep 确认没有与已有 Registry/System 功能重叠的新增代码
*   [ ] **API 引用正确**：所有引用的类、方法、接口在代码库中确实存在且签名匹配
*   [ ] **ECS 约束满足**：新组件 blittable、无 GC、QueryDescription 已缓存
*   [ ] **文档同步**：如果行为变更影响了已有文档，同一 commit 更新文档
*   [ ] **命名合规**：组件后缀（Cm/Tag/Event）、系统后缀（System）、命名不耦合业务

### 5.2 测试要求

| 变更类型 | 最低测试要求 |
|---------|------------|
| Core 新增 System | 单元测试覆盖核心逻辑 + 边界条件 |
| Core 新增 Registry | 注册/查询/冲突/容量边界测试 |
| GAS 相关变更 | GasTests 全量通过（`dotnet test src/Tests/GasTests/GasTests.csproj`） |
| Mod 新增/修改 | ModLauncher CLI build 通过 + 功能冒烟测试 |
| 架构边界变更 | ArchitectureTests 通过 |
| 导航相关变更 | Navigation2DTests 全量通过 |

测试风格遵循 `src/Tests/GasTests/TESTING_STYLE.md`。

## 6 AI Agent 专项规则

本节为 AI Agent（Cursor、Claude Code 等）提供强制性指引，防止幻觉代码和重复造轮子。

### 6.1 禁止凭空发明 API

AI Agent 生成代码时，以下行为视为严重违规：

*   调用代码库中不存在的方法或类
*   假设某个 Registry 有 `GetById` 方法但实际只有 `TryGet`
*   假设某个组件有某个字段但实际没有
*   使用 NuGet 包中不存在的重载

**规则**：每引用一个非 BCL 的类型或方法，必须先搜索确认其存在。搜索失败则不得使用。

### 6.2 禁止创建平行体系

AI Agent 不得在未经搜索的情况下创建以下内容：

*   新的 Registry 类（先确认 20+ 个已有 Registry 中是否有可复用的）
*   新的事件系统（先确认 `GameplayEventBus` 和 `TriggerManager` 是否满足需求）
*   新的配置加载机制（先确认 `ConfigPipeline` 是否支持）
*   新的组件基类或接口（先确认已有模式是否足够）

### 6.3 强制搜索 → 阅读 → 编码流程

```
搜索已有能力 → 阅读相关文档和源码 → 列出复用/新增清单 → 编码 → 验证 API 引用
```

不得跳过前三步直接编码。如果 Agent 在对话中未展示搜索和阅读过程，其产出的代码需要额外审查。

### 6.4 幻觉代码自检

AI Agent 在完成编码后，必须对自己生成的每个 `new` 构造、方法调用、类型引用执行一次存在性搜索。如果发现引用不存在的 API，立即修正，不得留给用户。

## 7 能力清单速查表

以下是仓库中已有的核心基础设施，新功能开发时优先在此基础上扩展，不要另起炉灶。

### 7.1 Registry 一览

| Registry | 位置 | 用途 |
|----------|------|------|
| `SystemFactoryRegistry` | `src/Core/Engine/` | System 工厂注册，Mod 通过此注册可选系统 |
| `AttributeRegistry` | `src/Core/Gameplay/GAS/Registry/` | 属性名 → ID 映射 |
| `TagRegistry` | `src/Core/Gameplay/GAS/Registry/` | Tag 名 → ID 映射 |
| `AttributeSinkRegistry` | `src/Core/Gameplay/GAS/Bindings/` | 属性 Sink 注册（跨层写入） |
| `EffectTemplateRegistry` | `src/Core/Gameplay/GAS/` | 效果模板 |
| `AbilityDefinitionRegistry` | `src/Core/Gameplay/GAS/` | 技能定义 |
| `OrderTypeRegistry` | `src/Core/Gameplay/GAS/Orders/` | 命令类型 |
| `PerformerDefinitionRegistry` | `src/Core/Presentation/` | 表现定义 |
| `MeshAssetRegistry` | `src/Core/Presentation/` | 网格资产 |
| `ComponentRegistry` | `src/Core/Config/` | 组件 JSON 反序列化 |
| `CameraControllerRegistry` | `src/Core/Gameplay/Camera/` | 相机控制器类型 |
| `LayerRegistry` | `src/Core/Layers/` | 层 ID |
| `BoardIdRegistry` | `src/Core/Map/Board/` | 棋盘 ID |
| `GraphProgramRegistry` | `src/Core/GraphRuntime/` | Graph 程序 |
| `FunctionRegistry` | `src/Core/Scripting/` | 脚本函数 |
| `TriggerDecoratorRegistry` | `src/Core/Scripting/` | Trigger 装饰器 |
| `TaskNodeRegistry` | `src/Core/Gameplay/AI/` | AI 任务节点 |
| `AtomRegistry` | `src/Core/Gameplay/AI/` | AI 世界状态原子 |
| `StringIntRegistry` | `src/Core/Registry/` | 通用字符串-整数双向映射 |

### 7.2 核心管线

| 管线 | 入口 | 文档 |
|------|------|------|
| ConfigPipeline | `ConfigPipeline.MergeGameConfig` | `07_config_pipeline.md` |
| GAS Effect Pipeline | `EffectRequestQueue` → 各 Phase System | `11_gas_layered_architecture.md` |
| Presentation Pipeline | Performer → ResponseChain | `06_presentation_performer.md` |
| Trigger Pipeline | `TriggerManager.OnEvent` | `08_trigger_guide.md` |
| Mod Loading | `ModLoader` → `IMod.OnLoad` | `02_mod_architecture.md` |
| Startup | `GameBootstrapper.InitializeFromBaseDirectory` | `09_startup_entrypoints.md` |

### 7.3 SystemGroup Phase 一览

```
SchemaUpdate → InputCollection → PostMovement → AbilityActivation →
EffectProcessing → AttributeCalculation → DeferredTriggerCollection →
Cleanup → EventDispatch → ClearPresentationFlags
```

新增 System 必须明确归属某个 phase，不得游离。

## 8 分支与 PR 工作流

### 8.1 分支命名

| 类型 | 格式 | 示例 |
|-----|------|------|
| 功能 | `feat/<scope>-<short-desc>` | `feat/gas-shield-system` |
| 修复 | `fix/<scope>-<short-desc>` | `fix/physics2d-collision-leak` |
| 重构 | `refactor/<scope>-<short-desc>` | `refactor/config-pipeline-merge` |
| 文档 | `docs/<short-desc>` | `docs/trigger-guide-update` |

### 8.2 PR 要求

每个 PR 必须包含：

1. **标题**：与 commit 格式一致（`<type>(<scope>): <description>`）
2. **描述**：包含第 3.1 节设计方案的简要版本，至少包含"挂靠点"和"复用/新增清单"
3. **测试证据**：测试通过的截图或日志
4. **文档同步**：如有架构文档需要更新，包含在同一 PR 中

### 8.3 Review 要点

Review 时优先检查以下项目：

*   是否存在与已有 Registry/System 重复的实现
*   是否引用了不存在的 API（幻觉代码）
*   是否遵循 ECS blittable/zero-GC 约束
*   是否正确归属 SystemGroup phase
*   命名是否耦合了具体业务

## 9 相关文档

*   文档编写规范：见 [00_documentation_standards.md](00_documentation_standards.md)
*   ECS 开发实践与 SoA 原则：见 [01_ecs_soa_principles.md](01_ecs_soa_principles.md)
*   Mod 架构与配置系统：见 [02_mod_architecture.md](02_mod_architecture.md)
*   GAS 分层架构与 Sink 最佳实践：见 [11_gas_layered_architecture.md](11_gas_layered_architecture.md)
*   Mod 运行时唯一真相与收束准则：见 [17_mod_runtime_single_source_of_truth.md](17_mod_runtime_single_source_of_truth.md)
*   版本收束处置矩阵：见 [18_convergence_disposition_matrix.md](18_convergence_disposition_matrix.md)
*   GAS 测试风格：见 `src/Tests/GasTests/TESTING_STYLE.md`
