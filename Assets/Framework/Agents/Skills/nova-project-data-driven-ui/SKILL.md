---
name: nova-project-data-driven-ui
description: Use when 项目组要把已接入或已明确表源的 Nova 数据表纳入加载链并绑定到一个业务 UIView、从既有入口打开并完成分阶段验证，且任务跨越表、UI、Prefab、注册与导出时使用。
---

# Nova 数据驱动业务界面

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

再读取 `references/contract.json`，并使用 `nova-project-integrate-table` 承担表接入/验证；先发现实际 View，已有页面复用 `nova-project-update-ui-view`，仅页面缺失且任务授权创建时使用 `nova-project-ui-create-view`；其中确定性表导出直接调用 `nova.project.table.export`，UI 注册导出共同复用 `nova.project.ui.export`。该 Workflow 只编排已有消费端能力，不直接改 Nova Manager、UIManager、表生成物或 Framework 包。

## 渐进式披露

- L0：先用 frontmatter、Catalog 与共同底线判断是否需要跨表和 UI 的编排。
- L1：只读取本 Skill、`references/contract.json` 及当前分支匹配的 `requires` Operation，冻结 Luban 表源、导出/加载描述、字段、View 与入口契约。
- L2：仅在表接入状态或 UI 写入边界不明确时，读取相应 Table/UI Docs 与项目事实；不要一次加载所有模块资料。
- L3：确认后先按表接入状态调用或复核 Table Operation，再调用既有页面更新或已授权的新页面创建/注册、Unity Editor/MCP 与`nova.project.ui.export`，遵守依赖图和单写者锁。
- L4：只收集本次数据绑定的编译与 Play smoke 证据；任何节点缺失时停止并返回 `partial` 或 `blocked`。

## 冻结业务契约

必须确认 Luban 表源、Schema、`ProjectId/DescriptionId`、导出范围、运行时加载描述与 Asset 地址、表类型、主键/选中规则、展示字段、缺行处理、语言与图标来源、目标 View/UIGroup、命名空间/程序集、Prefab Asset 地址、注册源、主页入口、返回行为和重复点击策略。禁止默认取“第一行”、硬编码表数据、在 UI 内重复加载表或替用户猜测导航语义。

先判断表是否已经纳入当前项目的运行时加载链，且可经公开 `Nova.Table.HasTable<T>()` / `GetTable<T>()` 使用。表源、Binding、加载时序或 UIGroup 不成立时，先在已确认范围内执行 `nova-project-integrate-table`；表源和运行时加载映射均未变化时，该 Operation 只复核并跳过导出。用户同时要求修改原标量或给已有自描述xlsx追加普通字段时，复用 Table Operation 的同源CLI分支，只写该表的精确原键/字段和值；复杂Schema、行键、公式或宏不因Workflow扩大支持。普通字段追加先正式导出该描述代码和数据并完成编译，再绑定View。表结构、代码或导出产物会变化时，必须先完成 Table Operation 再编译或绑定 View；本任务不因无关配置问题自动执行 Config 导出或修复。

已有 View、Prefab、注册和入口保持复用；只改变数据绑定时不重复创建页面或注册条目。`requires` 登记可组合 Operation 的依赖闭包，不要求把创建与更新两条分支都执行。选择页面分支后仍按对应 Operation 的保存、导出和验证契约完成。

## 依赖图

```text
冻结输入
 ├─ Table Operation：表源/设置 → 定向导出 → 运行时加载复核
 └─ UI Operation：View/注册源 → Prefab/导航写入
             ↓
   表产物稳定且确认门通过
             ↓
      绑定表数据与主页入口
             ↓
       UI Action Plan → 一次 Execute → 编译/重载 → Verify → Play smoke
```

只读发现可以并行；表结构、代码或导出产物会变化时，Table Operation 与依赖它的 UI 编译/绑定必须串行。任何 Unity、AssetDatabase、活动场景、表设置/导出输出或同一 UI 注册源写入必须串行。Pipify Batch 自身是顺序、失败即停的 Action Adapter，不能替代本图的并发与恢复判断。

UI 导出沿用页面 Operation 的契约：全量 `scope=all/code/data`；精确 `sourcePath` 仅 `code/data`。Plan 必须显示共享 `UITables`、`_configs` 和删除范围；单文件 `all` 不支持。新注册先导出 data 再 code，收到 `partial + Receipt` 后等待编译/重载并只读 Verify，不重放。

## 验证与恢复

验证定向表导出和运行时加载、入口按钮只触发一次、目标行字段值准确、缺表/缺 ID 有明确处理、页面关闭返回正常、池化重开会刷新数据。Play smoke 未执行时仅返回 `partial`；外部服务、真机和 CDN 结果不能由本 Workflow 推断。

每个写入节点记录精确输入、前置 hash 和局部验证结果。普通源码冲突停止并交给人工；导出使用已有事务/恢复能力；外部写入不自动重试或补偿。任何节点失败后不继续后续节点，也不把部分结果报告为 `success`。
