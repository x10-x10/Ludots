# Recipe: 新建 Mod

## 目标

创建一个独立 Mod，通过 `IMod.OnLoad` 注册 System 和 Trigger。

## 文件清单

```
mods/MyFeatureMod/
├── mod.json
├── MyFeatureMod.csproj
└── MyFeatureModEntry.cs
```

## mod.json

```json
{
  "name": "MyFeatureMod",
  "version": "1.0.0",
  "description": "一句话说明这个 Mod 做什么",
  "main": "bin/net8.0/MyFeatureMod.dll",
  "priority": 0,
  "dependencies": {
    "LudotsCoreMod": "^1.0.0"
  }
}
```

## 入口代码

```csharp
public class MyFeatureModEntry : IMod
{
    public void OnLoad(IModContext context)
    {
        // 注册可选 System（通过工厂，MapLoaded 时激活）
        context.SystemFactoryRegistry.Register(
            "MyFeatureSystem",
            world => new MyFeatureSystem(world),
            SystemGroup.PostMovement);

        // 注册事件处理
        context.OnEvent(GameEvents.GameStart, async ctx =>
        {
            var engine = ctx.GetEngine();
            var sfr = engine.ModLoader.SystemFactoryRegistry;
            sfr.TryActivate("MyFeatureSystem", ctx, engine);
        });
    }
}
```

## 挂靠点

| 基建 | 用途 |
|------|------|
| `SystemFactoryRegistry` | 注册 System 工厂 |
| `TriggerManager`（通过 `context.OnEvent`） | 注册 GameStart/MapLoaded 回调 |
| `ConfigPipeline` | 如果有配置，放 `assets/Configs/` 目录，自动参与合并 |

## 检查清单

*   [ ] `mod.json.main` 指向 `bin/net8.0/*.dll`
*   [ ] 入口类实现 `IMod.OnLoad(IModContext)`，无静态构造器
*   [ ] Mod 目录在 `mods/`，不在 `src/`
*   [ ] `dependencies` 声明了所有必需的 Mod

参考：`mods/DiagnosticsOverlayMod/`、`mods/GmConsoleMod/`
