# 文档治理规范

本篇定义 Ludots 仓库文档的正式分层、命名和维护规则，确保 `docs/` 结构稳定、单点归属明确、路径可追踪。

## 1 文档分层

Ludots 文档按职责固定分为六类：

| 目录 | 内容 | 是否 SSOT |
|------|------|-----------|
| `docs/conventions/` | 开发规范、流程、硬约束 | 是 |
| `docs/architecture/` | 当前架构设计、模块关系、数据流 | 是 |
| `docs/reference/` | 事实查表、契约、操作手册 | 是 |
| `docs/adr/` | 架构决策记录 | 否 |
| `docs/audits/` | 审计、验收、收束证据 | 否 |
| `docs/rfcs/` | 提案 | 否 |

**规则**：一个事实只能在一个 SSOT 文档中定义。其他文档只能引用，不能重复定义。

## 2 禁止出现的内容

### 2.1 正文中的兼容与历史说明

文档正文只描述当前行为。以下内容禁止进入正式正文：

*   兼容入口、兼容路径、兼容目录。
*   “旧版路径仍可用”之类的说明。
*   “后续再迁移”“先这样放着”的临时描述。

历史信息通过 Git 历史、Tag、PR 和 ADR 查询，不在正文中保留。

### 2.2 不可验证的描述

以下内容禁止：

*   没有代码或文档路径支撑的结论。
*   推测性描述和未实现计划。
*   把审计结论直接写成正式规范。

### 2.3 内部噪音

以下内容禁止：

*   开发阶段编号、临时里程碑代号。
*   AI 工具署名。
*   依赖内部讨论语境才能理解的表述。

## 3 命名与入口

### 3.1 目录入口

*   `docs/README.md` 是全局唯一入口。
*   每个一级目录必须有自己的 `README.md`。
*   新增、删除或重命名文档时，必须同步更新所属目录的 `README.md`；若影响全局导航，还必须同步更新 `docs/README.md`。

### 3.2 文件命名

*   `docs/conventions/` 沿用编号式命名：`NN_topic.md`。
*   `docs/architecture/`、`docs/reference/`、`docs/audits/` 使用语义化 `snake_case.md`。
*   `docs/adr/` 使用 `ADR-XXXX-topic.md`。
*   `docs/rfcs/` 使用 `RFC-XXXX-topic.md`。

编号或语义名一旦发布，不因排序偏好随意重命名。

## 4 文档结构

### 4.1 整体骨架

```markdown
# 标题（与文件名语义一致）

一段话说明本篇解决什么问题、覆盖哪些内容。

## 1 第一个主题

### 1.1 子主题

正文、代码示例、图表。

## N 相关文档

*   交叉引用列表
```

### 4.2 交叉引用

*   Markdown 链接使用相对路径。
*   源码和文档路径引用使用仓库相对路径，如 `src/...`、`docs/...`。
*   不使用绝对本地路径和 `file://` 链接。

**示例**：

```markdown
*   启动顺序与入口点：见 [../architecture/startup_entrypoints.md](../architecture/startup_entrypoints.md)
*   参考实现：`src/Core/Scripting/TriggerManager.cs`
```

## 5 语言与证据

*   所有正文使用中文，技术名词保留英文原文。
*   结论优先附源码路径、测试路径或相关文档路径。
*   审计和 RFC 只能作为证据来源，不能替代正式规范。

## 6 维护规则

### 6.1 同提交更新

当代码变更影响文档描述的行为时，**同一个提交或同一个 PR** 必须同步更新文档。不接受“先改代码后补文档”。

### 6.2 删除过时内容

如果某个机制被移除，直接删除对应描述。不要加删除线、废弃标记或兼容说明。

### 6.3 审计与提案回写

*   审计结论如需成为正式规则，回写到 `docs/conventions/`。
*   审计结论如需成为正式设计，回写到 `docs/architecture/` 或 `docs/reference/`。
*   RFC 被接受后，必须回写正式文档，再关闭 RFC。

### 6.4 Git 审核归属

*   文档目录的审核责任通过 `.github/CODEOWNERS` 管理。
*   `docs/conventions/`、`docs/architecture/`、`docs/reference/`、`docs/adr/`、`docs/audits/`、`docs/rfcs/` 的 owner 必须显式配置。
*   文档治理脚本和 PR 模板的 owner 也必须显式配置，避免规则变更绕过文档审核。

## 7 检查清单

提交文档前逐项确认：

*   [ ] 所属层级明确，且没有跨层重复定义。
*   [ ] 不含兼容入口、兼容路径和历史迁移说明。
*   [ ] 不含推测性或未实现内容。
*   [ ] 路径均为仓库相对路径，且目标存在。
*   [ ] 所属目录 `README.md` 已同步；若影响全局导航，`docs/README.md` 已同步。

## 8 相关文档

*   文档总览：见 [../README.md](../README.md)
*   开发规范总索引：见 [README.md](README.md)
*   架构文档总索引：见 [../architecture/README.md](../architecture/README.md)
*   参考资料总索引：见 [../reference/README.md](../reference/README.md)
