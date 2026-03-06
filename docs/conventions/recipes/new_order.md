# Recipe: 新增交互/命令类型

## 目标

新增一种输入 → 命令的映射，让玩家通过按键或点击触发 gameplay 行为。

## 文件清单

```
mods/MyMod/assets/Input/
└── input_order_mappings.json   ← 输入 → 命令映射
```

以及在 Trigger 中注册 OrderType 和 TagKeyResolver。

## 输入映射（JSON）

```json
{
  "mappings": [
    {
      "actionId": "Dodge",
      "trigger": "PressedThisFrame",
      "orderTagKey": "dodge",
      "requireSelection": false,
      "modifierBehavior": "QueueOnModifier"
    }
  ]
}
```

## 注册 OrderType（在 Trigger 中）

```csharp
// GameStart trigger 中
var orderTypeRegistry = engine.OrderTypeRegistry;
int dodgeTagId = TagRegistry.Register("Order.Active.Dodge");

orderTypeRegistry.Register(new OrderTypeConfig
{
    OrderTagId = dodgeTagId,
    Label = "dodge",
    MaxQueueSize = 1,
    SameTypePolicy = SameTypePolicy.Replace,
    Priority = 80
});
```

## 注册 TagKeyResolver

```csharp
inputOrderMapping.SetTagKeyResolver(key => key switch
{
    "dodge" => dodgeTagId,
    /* ... */
    _ => 0
});
```

## 挂靠点

| 基建 | 用途 |
|------|------|
| `OrderTypeRegistry` | 命令类型配置 |
| `InputOrderMappingSystem` | 输入 → 命令转换 |
| `TagRegistry` | 命令 tag ID |
| `OrderBuffer`（ECS 组件） | 命令队列，由 `OrderBufferSystem` 处理 |

## 检查清单

*   [ ] `OrderTagId` 通过 `TagRegistry.Register` 获取，不硬编码数字
*   [ ] `Label` 与 JSON 中的 `orderTagKey` 一致
*   [ ] TagKeyResolver 覆盖了所有 mapping 中的 key
*   [ ] 不新建命令处理系统——使用已有的 `OrderBufferSystem` + `AbilitySystem` 管线

参考：`mods/MobaDemoMod/assets/Input/input_order_mappings.json`、`mods/MobaDemoMod/Triggers/InstallMobaDemoOnGameStartTrigger.cs`
