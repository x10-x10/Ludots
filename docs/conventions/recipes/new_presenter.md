# Recipe: 新增表现/UI Performer

## 目标

新增一个 Performer 定义，响应 gameplay 事件产生视觉表现（粒子、标记、音效等）。

## 方式 A：JSON 配置（推荐）

```
mods/MyMod/assets/Configs/Presentation/
└── performers.json
```

```json
[
  {
    "id": "MyMod.DamageFlash",
    "visualKind": "Marker3D",
    "meshOrShapeId": "sphere",
    "defaultColor": [1, 0, 0, 0.8],
    "defaultScale": 0.4,
    "defaultLifetime": 0.15,
    "alphaFadeOverLifetime": true,
    "rules": [
      {
        "event": { "kind": "EffectApplied", "keyId": -1 },
        "command": { "commandKind": "CreatePerformer" }
      }
    ]
  }
]
```

## 方式 B：代码注册

```csharp
// 在 Mod OnLoad 或 GameStart trigger 中
var registry = engine.PerformerDefinitionRegistry;
int id = registry.GetOrRegisterId("MyMod.HitMarker");
registry.Register("MyMod.HitMarker", new PerformerDefinition
{
    VisualKind = PerformerVisualKind.Marker3D,
    DefaultLifetime = 0.2f,
    Rules = new[]
    {
        new PerformerRule
        {
            Event = new EventFilter
            {
                Kind = PresentationEventKind.EffectApplied,
                KeyId = -1
            },
            Command = new PerformerCommand
            {
                CommandKind = PresentationCommandKind.CreatePerformer,
                PerformerDefinitionId = id
            }
        }
    }
});
```

## 挂靠点

| 基建 | 用途 |
|------|------|
| `PerformerDefinitionRegistry` | Performer 定义注册 |
| `PerformerDefinitionConfigLoader` | 从 JSON 自动加载 |
| `PresentationEventStream` | gameplay 事件到表现层的桥接 |
| `ResponseChain` | Performer 命令执行链 |

## 检查清单

*   [ ] `id` 全局唯一，使用 `ModName.PerformerName` 格式
*   [ ] 优先使用 JSON 配置，只有需要动态逻辑时才用代码注册
*   [ ] 不在 Performer 中修改 gameplay 状态——表现层只读
*   [ ] Adapter 层（Raylib）负责实际渲染，Core 只定义数据

参考：`src/Core/Presentation/Performers/BuiltinPerformerDefinitions.cs`
