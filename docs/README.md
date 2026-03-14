# 文档总览

`docs/` 是 Ludots 文档的唯一正式入口。`main` 分支上的内容即当前真相；历史版本、迁移过程和已废弃设计通过 Git 历史、PR 和 ADR 追溯，不在正文中保留兼容说明。

## 1 分层结构

| 目录 | 角色 | 是否 SSOT |
|------|------|-----------|
| `docs/conventions/` | 开发规范、流程、硬约束 | 是 |
| `docs/architecture/` | 当前架构设计、模块边界、数据流 | 是 |
| `docs/reference/` | 事实查表、契约、操作手册 | 是 |
| `docs/adr/` | 架构决策记录（为什么这样定） | 否，记录决策 |
| `docs/audits/` | 审计、验收、收束与回顾证据 | 否，记录证据 |
| `docs/rfcs/` | 提案与讨论稿 | 否，记录候选方案 |

## 2 阅读入口

*   [开发规范](conventions/README.md) —— 编码标准、Feature 工作流、AI 辅助开发规则、环境与构建、文档治理。
*   [架构文档](architecture/README.md) —— Core、Runtime、Gameplay、Presentation 等当前设计。
*   [参考资料](reference/README.md) —— CLI 操作、配置合并实践、相机标准、3C 能力清单、外部依赖入口。
*   [架构决策](adr/README.md) —— 关键决策与收敛原因。
*   [审计记录](audits/README.md) —— 审计、验收、收束矩阵和阶段性报告。
*   [RFC 提案](rfcs/README.md) —— 尚未纳入正式规范的提案。

## 3 使用规则

*   规则性内容只能定义在 `docs/conventions/`。
*   当前实现只能定义在 `docs/architecture/` 或 `docs/reference/` 的一个位置，不得重复描述。
*   `docs/audits/` 与 `docs/rfcs/` 不得反向成为规范来源。
*   代码行为变更时，同一提交或同一 PR 必须同步更新对应文档。

## 4 相关文档

*   文档治理规范：见 [conventions/04_documentation_governance.md](conventions/04_documentation_governance.md)
*   开发规范总索引：见 [conventions/README.md](conventions/README.md)
*   架构文档总索引：见 [architecture/README.md](architecture/README.md)
*   参考资料总索引：见 [reference/README.md](reference/README.md)
