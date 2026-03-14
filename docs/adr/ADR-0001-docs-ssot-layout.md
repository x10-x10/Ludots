# ADR-0001 文档 SSOT 分层结构

本记录定义 Ludots 文档的正式目录分层，目标是消除兼容入口、重复定义和路径漂移，让规则、设计、查表、决策和审计各归其位。

## 1 背景

重组前，`docs/` 同时存在 `README.md` 兼容入口、`developer-guide/` 旧名称、`arch-guide/` 兼容入口和混合型文档目录。规则、架构、操作手册和审计报告混放在一起，导致以下问题：

*   入口不唯一，AI Agent 和开发者容易从不同路径进入。
*   审计和设计文档混排，SSOT 边界不清晰。
*   文档路径含历史命名，目录语义弱于内容语义。

## 2 决策

采用如下正式分层：

*   `docs/conventions/`：开发规范与流程，唯一规则来源。
*   `docs/architecture/`：当前架构设计与数据流，唯一设计来源。
*   `docs/reference/`：事实查表和操作手册，唯一查表来源。
*   `docs/adr/`：架构决策记录。
*   `docs/audits/`：审计、验收与收束证据。
*   `docs/rfcs/`：提案区。

同时取消以下旧做法：

*   不再保留兼容入口。
*   不在正文中保留历史/兼容路径说明。
*   不使用 `developer-guide` 和 `arch-guide` 作为正式目录名。

## 3 影响

*   `docs/README.md` 成为全局唯一入口。
*   旧 `docs/developer-guide/` 文档迁移到 `docs/architecture/`、`docs/reference/`、`docs/audits/`、`docs/conventions/`。
*   根目录说明、Agent 配置和交叉引用统一切到新路径。

## 4 后续约束

*   代码行为变更必须与对应文档同 PR 更新。
*   审计/RFC 不得反向成为规范来源。
*   历史信息通过 Git 历史、Tag、PR 和 ADR 查询，不在正文里做兼容描述。

## 5 相关文档

*   文档总览：见 [../README.md](../README.md)
*   文档治理规范：见 [../conventions/04_documentation_governance.md](../conventions/04_documentation_governance.md)
