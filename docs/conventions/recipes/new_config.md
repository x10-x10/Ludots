# Recipe: 新增配置类型

## 目标

新增一种 JSON 配置类型，通过 ConfigPipeline 参与 Mod 间合并。

## 文件清单

```
assets/Configs/config_catalog.json   ← 追加条目（Core 或 Mod）
mods/MyMod/assets/MyConfigs/
└── my_items.json                    ← 配置数据
```

## 在 config_catalog.json 中注册

```json
{ "Path": "MyConfigs/my_items.json", "Policy": "ArrayById", "IdField": "id" }
```

合并策略：

| Policy | 行为 | 适用场景 |
|--------|------|---------|
| `Singleton` | 对象字段递归合并 | 全局唯一配置（game settings） |
| `ArrayById` | 按 `IdField` 键控合并/覆盖 | 列表型配置（技能、效果、道具） |
| `ArrayAppend` | 数组追加 | 累加型配置（事件列表） |
| `ArrayReplace` | 数组整体替换 | 不可合并的数组 |

## 配置数据（my_items.json）

```json
[
  { "id": "Item.Sword", "damage": 10, "weight": 5 },
  { "id": "Item.Shield", "armor": 20, "weight": 8 }
]
```

## 在代码中加载

```csharp
// 在 Loader 类中（随 ConfigPipeline 执行）
var entry = ConfigPipeline.GetEntryOrDefault(catalog, "MyConfigs/my_items.json", ConfigMergePolicy.ArrayById, "id");
var merged = pipeline.MergeArrayByIdFromCatalog(in entry, report);
for (int i = 0; i < merged.Count; i++)
{
    var node = merged[i].Node;
    var id = merged[i].Id;
    /* 解析并注册到 Registry */
}
```

## 挂靠点

| 基建 | 用途 |
|------|------|
| `ConfigCatalog` | 配置条目注册 |
| `ConfigPipeline` | 多 Mod 合并 |
| `ConfigCatalogLoader` | 加载 `config_catalog.json` |

## 检查清单

*   [ ] `IdField` 显式指定为 `"id"`，不依赖隐式 fallback
*   [ ] `id` 字段为字符串，全局唯一
*   [ ] 使用 `ConfigPipeline` 合并，不自建 JSON 加载器
*   [ ] Mod 覆盖 Core 配置时，只需在 Mod 的同路径放同名文件

参考：`assets/Configs/config_catalog.json`、`docs/developer-guide/07_config_pipeline.md`
