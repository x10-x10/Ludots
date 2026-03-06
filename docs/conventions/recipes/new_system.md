# Recipe: 新增 ECS System

## 目标

新建一个 System 并注册到正确的 SystemGroup phase。

## 最小代码

```csharp
public sealed class MyFeatureSystem : BaseSystem<World, float>
{
    // QueryDescription 缓存为 static readonly，不在 Update 中创建
    private static readonly QueryDescription _query = new QueryDescription()
        .WithAll<WorldPositionCm, Velocity>();

    private readonly CommandBuffer _cmd;

    public MyFeatureSystem(World world) : base(world)
    {
        _cmd = new CommandBuffer();
    }

    public override void Update(in float dt)
    {
        // 零 GC：不分配托管对象，不用 LINQ
        World.Query(in _query, (ref WorldPositionCm pos, ref Velocity vel) =>
        {
            pos.Value += vel.Direction * vel.Speed * (Fix64)dt;
        });

        // 结构变更通过 CommandBuffer，不在 query 内直接操作
        _cmd.Playback(World);
    }
}
```

## 注册方式

**方式 A：Core 系统（引擎内置）**

```csharp
// GameEngine.InitializeCoreSystems 中
engine.RegisterSystem(new MyFeatureSystem(world), SystemGroup.PostMovement);
```

**方式 B：Mod 可选系统（推荐）**

```csharp
// IMod.OnLoad 中
context.SystemFactoryRegistry.Register(
    "MyFeatureSystem",
    world => new MyFeatureSystem(world),
    SystemGroup.PostMovement);
```

## 选择 SystemGroup

| Phase | 适用场景 |
|-------|---------|
| `InputCollection` | 输入采集、命令缓冲 |
| `PostMovement` | 位移后的碰撞检测、空间更新 |
| `AbilityActivation` | 技能激活、命令处理 |
| `EffectProcessing` | 效果提议/应用/移除 |
| `AttributeCalculation` | 属性聚合 |
| `Cleanup` | 死亡清理、过期组件移除 |

## 检查清单

*   [ ] `QueryDescription` 为 `private static readonly`
*   [ ] `Update` 内零 GC、无 LINQ
*   [ ] 结构变更通过 `CommandBuffer`
*   [ ] gameplay 值用 `Fix64`，不用 `float`
*   [ ] 明确归属一个 SystemGroup phase

参考：`src/Core/Gameplay/GAS/Systems/StopOrderSystem.cs`
