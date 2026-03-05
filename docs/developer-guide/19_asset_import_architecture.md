# 资产导入架构与自定义模型支持

本篇分析 Ludots 六边形架构下资产导入的现状，评估自定义模型（如 .obj、.gltf）的可行性，并指出架构缺口与改进方向。

## 1 现状概览

### 1.1 资产相关组件

| 组件 | 位置 | 职责 |
|------|------|------|
| `MeshAssetRegistry` | Core/Presentation/Assets | 将 MeshAssetId 映射为 PrimitiveMeshKind（Cube、Sphere） |
| `VisualModel` | Core/Presentation/Components | 实体视觉定义：MeshId、MaterialId、BaseScale |
| `PrimitiveDrawBuffer` | Core/Presentation/Rendering | 调试/特效图元输出（位置、缩放、颜色、MeshAssetId） |
| `TransientMarkerBuffer` | Core/Presentation/Rendering | 瞬态标记（点击、技能特效等），输出到 PrimitiveDrawBuffer |

### 1.2 当前资产表能力

`MeshAssetRegistry` 仅支持**内置图元**：

```csharp
// PrimitiveMeshAssetIds
Cube = 1, Sphere = 2

// MeshAssetRegistry.TryGetPrimitiveKind(meshAssetId, out kind)
// 返回 PrimitiveMeshKind.Cube 或 Sphere，否则 false
```

无路径、无 VFS URI、无自定义模型注册接口。

### 1.3 Raylib 渲染管线

| 输出源 | 消费方 | 资产来源 |
|--------|--------|----------|
| PrimitiveDrawBuffer | RaylibPrimitiveRenderer | GenMeshCube / GenMeshSphere 运行时生成 |
| GroundOverlayBuffer | RaylibHostLoop.DrawGroundOverlays | 无 mesh，纯几何绘制 |
| VertexMap | RaylibTerrainRenderer | VertexMapChunkMeshBuilder 程序化生成 |
| DebugDrawCommandBuffer | RaylibDebugDrawRenderer | 线框 |

**关键发现**：Raylib 从未调用 `LoadModel` 或从文件加载 mesh。所有 3D 几何均为程序化生成。

### 1.4 VisualModel 实体渲染

Core 定义了 `VisualModel`（MeshId、MaterialId、BaseScale），`WorldToVisualSyncSystem` 将 `WorldPositionCm` 同步到 `VisualTransform`。但 **Raylib 宿主循环中不存在任何遍历 `VisualModel + VisualTransform` 实体的渲染器**。

即：带 VisualModel 的实体目前不会被绘制。

## 2 六边形架构下的资产边界

适配器文档（03_adapter_pattern.md）约定：

*   **Core**：不依赖平台库，仅依赖抽象接口；输出渲染指令或同步状态。
*   **Adapter**：负责将平台数据（如 `Texture2D`、`Model`）与 Core 通用格式（如 `ResourceHandle`、MeshAssetId）转换。

资产导入的合理分工应为：

| 层级 | 职责 |
|------|------|
| Core | 资产表（MeshId → 逻辑标识或 VFS URI）；VisualModel 引用 MeshId；不持有平台资源 |
| Adapter | 根据 VFS URI 加载平台资源（Model/Mesh）；缓存平台句柄；渲染时 MeshId → 平台资源 |

当前实现中，Core 的 `MeshAssetRegistry` 只映射到 `PrimitiveMeshKind`，没有“逻辑标识或 VFS URI”这一层，Adapter 无法扩展自定义模型。

## 3 架构缺口

### 3.1 资产表不支持自定义模型

`MeshAssetRegistry` 仅支持 `TryGetPrimitiveKind`，无法：

*   注册路径型资产（如 `MobaDemoMod:Models/hero.obj`）
*   区分“图元”与“文件模型”
*   让 Adapter 知道从何处加载

### 3.2 无资产加载管线

*   **配置层**：无 mesh 资产清单（如 `meshes.json`）供 ConfigPipeline 合并。
*   **加载层**：无统一入口在 MapLoaded/GameStart 时从 VFS 加载模型。
*   **运行时**：Adapter 无平台资源缓存（MeshId → Raylib Model/Mesh）。

### 3.3 实体网格渲染缺失

RaylibHostLoop 未实现：

*   查询 `VisualModel + VisualTransform + CullState` 实体
*   将 MeshId 解析为平台 mesh
*   按位置、缩放、旋转绘制

因此即使引入自定义模型，带 VisualModel 的实体仍无法显示。

### 3.4 VFS 与 Adapter 未打通

VFS 已支持 `GetStream(uri)`、`TryResolveFullPath(uri)`，可用于读取模型文件。但：

*   Core 未定义“MeshId → VFS URI”的资产表
*   Adapter 未实现“VFS URI → Raylib Model”的加载逻辑

## 4 改进方案

### 4.1 扩展资产表（Core）

在保持 Primitive 兼容的前提下，扩展 `MeshAssetRegistry`：

```csharp
// 资产类型：图元 | 文件模型
public enum MeshAssetKind { None, Primitive, File }

public struct MeshAssetEntry
{
    public int MeshAssetId;
    public MeshAssetKind Kind;
    public PrimitiveMeshKind PrimitiveKind;  // 当 Kind == Primitive 时有效
    public string VfsUri;                    // 当 Kind == File 时有效，如 "MobaDemoMod:Models/hero.obj"
}
```

或采用更松耦合的设计：Core 仅维护 `MeshId → VfsUri` 的配置表（来自 ConfigPipeline），Primitive 使用特殊 URI（如 `builtin://cube`）或保留现有 PrimitiveMeshKind 分支。

### 4.2 配置驱动的资产清单

通过 ConfigPipeline 合并各 Mod 的 mesh 清单，例如：

```json
// Core:Configs/meshes.json 或 Mod:assets/meshes.json
{
  "meshes": [
    { "id": 1, "kind": "primitive", "primitive": "cube" },
    { "id": 2, "kind": "primitive", "primitive": "sphere" },
    { "id": 100, "kind": "file", "uri": "MobaDemoMod:Models/hero.obj" }
  ]
}
```

Core 在初始化时构建 `MeshAssetRegistry`（或等价资产表），Adapter 在 MapLoaded 时读取该表，对 `kind == "file"` 的条目通过 VFS 加载并缓存。

### 4.3 Adapter 侧资产加载接口

在 Adapter 层定义平台资源加载与缓存：

*   **加载**：接收 VFS URI，调用 `VFS.GetStream(uri)` 或 `TryResolveFullPath`，使用 Raylib `LoadModel(path)` 加载。
*   **缓存**：`Dictionary<int, Model>` 或 `Dictionary<int, Mesh>`，键为 MeshAssetId。
*   **生命周期**：MapLoaded 时加载当前 Map 所需资产，MapUnloaded 时释放；或采用全局缓存按需加载。

Raylib 支持 `.obj`、`.gltf` 等格式，需确保 VFS 解析出的物理路径可被 Raylib 原生 API 使用（`TryResolveFullPath` 已满足）。

### 4.4 实体网格渲染器

在 Raylib 客户端新增 `RaylibEntityMeshRenderer`（或等效组件）：

1.  每帧查询 `VisualModel + VisualTransform + CullState` 且 `IsVisible` 的实体
2.  对每个实体：`MeshId` → 平台缓存中的 Model/Mesh
3.  使用 `VisualTransform.Position/Rotation/Scale` 和 `VisualModel.BaseScale` 计算变换矩阵
4.  调用 `DrawModel` 或 `DrawMesh`

该渲染器属于 Adapter 层，依赖 `MeshAssetRegistry`（或扩展后的资产表）和平台资源缓存。

### 4.5 数据流示意

```
ConfigPipeline 合并 meshes.json
  → MeshAssetRegistry 构建 (MeshId → Kind, PrimitiveKind | VfsUri)
  → Adapter 在 MapLoaded 时：遍历 File 型条目，VFS.GetStream/TryResolveFullPath → LoadModel → 缓存
  → 渲染帧：PrimitiveDrawBuffer / 实体 VisualModel
      → MeshAssetRegistry 解析 MeshId
      → 图元：GenMeshCube/Sphere（现有逻辑）
      → 文件：缓存中的 Model → DrawModel
```

## 5 结论与建议

### 5.1 当前架构评估

| 维度 | 结论 |
|------|------|
| 六边形边界 | Core 未持有平台类型，符合；但资产表过窄，无法表达自定义模型 |
| 资产导入流程 | 不完整：无配置、无加载、无实体渲染 |
| 自定义模型可行性 | 架构上可行，需补齐资产表、配置、加载、实体渲染四块 |

### 5.2 建议实施顺序

1.  **扩展 MeshAssetRegistry**：支持 File 型资产与 VfsUri，保持 Primitive 兼容。
2.  **引入 meshes 配置**：ConfigPipeline 合并，GameEngine 初始化时填充资产表。
3.  **Adapter 资产加载**：MapLoaded 时从 VFS 加载 File 型 mesh，建立 MeshId → 平台资源缓存。
4.  **实体网格渲染器**：Raylib 侧实现 VisualModel 实体绘制，接入 HostLoop。
5.  **RaylibPrimitiveRenderer 扩展**：对 File 型 MeshAssetId，从缓存取 Model 绘制，替代仅 Cube/Sphere 的局限。

### 5.3 注意事项

*   **线程与异步**：模型加载可能耗时，需考虑异步加载与占位符，避免阻塞主循环。
*   **MaterialId**：VisualModel.MaterialId 当前未被使用，若需支持自定义材质，需同步设计材质资产表与加载。
*   **LOD 与剔除**：CameraCullingSystem 已产出 CullState，实体渲染器应尊重 IsVisible 与 LOD。

## 6 相关文档

*   适配器原则与平台抽象：见 [03_adapter_pattern.md](03_adapter_pattern.md)
*   表现管线与 Performer 体系：见 [06_presentation_performer.md](06_presentation_performer.md)
*   Mod 架构与 VFS：见 [02_mod_architecture.md](02_mod_architecture.md)
*   ConfigPipeline 合并：见 [07_config_pipeline.md](07_config_pipeline.md)
