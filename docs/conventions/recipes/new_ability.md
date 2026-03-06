# Recipe: 新增 GAS 技能

## 目标

通过 JSON 配置定义一个新技能，挂靠到已有的 GAS 管线。

## 文件清单

```
mods/MyMod/assets/GAS/
├── abilities.json   ← 技能定义（时间轴）
└── effects.json     ← 效果模板（数值）
```

## 效果模板（effects.json）

```json
[
  {
    "id": "Effect.MyMod.Damage.Q",
    "durationType": "Instant",
    "modifiers": [
      { "attribute": "Health", "op": "Add", "value": -50 }
    ]
  }
]
```

## 技能定义（abilities.json）

```json
[
  {
    "id": "Ability.MyMod.SkillQ",
    "exec": {
      "clockId": "FixedFrame",
      "items": [
        { "kind": "TagClip", "tick": 0, "duration": 15, "tag": "Cooldown.GCD" },
        { "kind": "TagClip", "tick": 0, "duration": 180, "tag": "Cooldown.Skill.Q" },
        { "kind": "EffectSignal", "tick": 0, "template": "Effect.MyMod.Damage.Q" },
        { "kind": "End", "tick": 0 }
      ]
    },
    "blockTags": {
      "blockedAny": ["Cooldown.GCD", "Cooldown.Skill.Q"]
    }
  }
]
```

## 挂靠点

| 基建 | 用途 |
|------|------|
| `AbilityDefinitionRegistry` | 技能定义注册（由 `AbilityExecLoader` 从 JSON 自动加载） |
| `EffectTemplateRegistry` | 效果模板注册（由 `EffectTemplateLoader` 自动加载） |
| `TagRegistry` | 技能 ID 和 Cooldown tag 自动注册 |
| `ConfigPipeline` | JSON 文件通过 `config_catalog.json` 的 `ArrayById` 策略合并 |

## 检查清单

*   [ ] `id` 全局唯一，使用 `Domain.Mod.Category.Name` 命名（如 `Ability.MyMod.SkillQ`）
*   [ ] effect 的 `attribute` 名在 `AttributeRegistry` 中已注册
*   [ ] tag 名不与其他 Mod 冲突
*   [ ] 不写 C# 代码——技能通过 JSON 配置驱动，不需要新建类

参考：`mods/MobaDemoMod/assets/GAS/`
