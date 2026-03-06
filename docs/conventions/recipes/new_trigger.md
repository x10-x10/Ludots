# Recipe: 新增事件触发器

## 目标

在 Mod 中注册事件回调，响应 GameStart、MapLoaded 等生命周期事件。

## 方式 A：内联 handler（简单场景）

```csharp
// IMod.OnLoad 中
context.OnEvent(GameEvents.MapLoaded, async ctx =>
{
    var engine = ctx.GetEngine();
    var mapId = ctx.Get<string>("MapId");
    engine.ModLoader.SystemFactoryRegistry.TryActivate("MySystem", ctx, engine);
});
```

## 方式 B：Trigger 类（复杂场景）

```csharp
public sealed class MySetupTrigger : Trigger
{
    private readonly IModContext _ctx;

    public MySetupTrigger(IModContext ctx)
    {
        _ctx = ctx;
        EventKey = GameEvents.GameStart;
    }

    public override async Task ExecuteAsync(ScriptContext ctx)
    {
        var engine = ctx.GetEngine();
        /* 注册 System、初始化配置等 */
    }
}

// OnLoad 中注册
context.OnEvent(GameEvents.GameStart, new MySetupTrigger(context).ExecuteAsync);
```

## 常用事件

| EventKey | 触发时机 | 典型用途 |
|----------|---------|---------|
| `GameEvents.GameStart` | 引擎启动后 | 注册 System、加载配置 |
| `GameEvents.MapLoaded` | 地图加载完成 | 地图专属 System 激活 |

## 挂靠点

| 基建 | 用途 |
|------|------|
| `TriggerManager`（通过 `context.OnEvent`） | 事件回调注册 |
| `ScriptContext` | 事件上下文，携带 Engine、MapId 等 |
| `SystemFactoryRegistry.TryActivate` | 在 trigger 中激活可选 System |

## 检查清单

*   [ ] 通过 `context.OnEvent` 注册，不直接操作 `TriggerManager` 内部
*   [ ] 使用 `GameEvents` 中已有的事件 key，不自建事件 key（除非确认不存在）
*   [ ] handler 中不阻塞——使用 `async`/`await`
*   [ ] 幂等：同一事件多次触发不应产生重复注册（用 guard key 检查）

参考：`mods/MobaDemoMod/MobaDemoModEntry.cs`、`mods/MobaDemoMod/Triggers/InstallMobaDemoOnGameStartTrigger.cs`
