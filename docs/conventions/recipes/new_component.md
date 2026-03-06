# Recipe: 新增 ECS Component

## 目标

新建一个 blittable struct 组件，用于 Arch ECS 实体。

## 三种类型

### 数据组件（后缀 `Cm`）

存储 gameplay 数据，有字段。

```csharp
public struct HealthCm
{
    public Fix64 Current;
    public Fix64 Max;
}
```

### 标签组件（后缀 `Tag`）

零字段标记，用于 query 过滤。

```csharp
public struct IsDeadTag { }
```

### 事件组件（后缀 `Event`）

帧内临时数据，由 System 产生，下一帧清除。

```csharp
public struct DamageEvent
{
    public Entity Source;
    public Entity Target;
    public Fix64 Amount;
}
```

## Blittable 规则

| 允许 | 禁止 |
|------|------|
| `int`, `float`, `Fix64`, `Fix64Vec2` | `string` |
| `Entity`（Arch 值类型） | `class` 引用 |
| `bool`（blittable） | `List<T>`, `Dictionary<K,V>` |
| 固定大小 buffer（`unsafe fixed`） | `object`, `dynamic` |
| 其他 blittable struct | 任何引用类型字段 |

## 文件位置

| 组件归属 | 位置 |
|---------|------|
| 通用（多子模块共用） | `src/Core/Components/` |
| 子模块专用 | 子模块目录下 `Components/`（如 `src/Core/Ludots.Physics2D/Components/`） |
| Mod 专用 | Mod 目录内 |

## 检查清单

*   [ ] struct，不是 class
*   [ ] 所有字段为 blittable 值类型
*   [ ] gameplay 数值使用 `Fix64`/`Fix64Vec2`
*   [ ] 后缀正确：`Cm` / `Tag` / `Event`
*   [ ] 命名不耦合业务（`HealthCm` 而非 `MobaHealthCm`）

参考：`src/Core/Components/Components.cs`
