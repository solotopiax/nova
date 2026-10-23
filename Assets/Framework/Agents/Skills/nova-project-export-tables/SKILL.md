---
name: nova-project-export-tables
description: Use when 项目组只需按有效 TableSettings 导出 Table 代码或数据，或先调整源数据、追加普通字段再导出并核验产物时使用。
---

# Nova 导出 Tables

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

先读取 `references/contract.json`，再通过 `nova_project_action` 对 `nova.project.table.export` 执行 `describe`。仅在 Action 报告配置或范围不明确时，才读取 `Docs/Editor/EditorUtil/EditorUtil.Table/EditorUtil.Table.Exporter.md` 和相关 TableSettings 文档；不要为一次明确导出重新审计完整表接入链。

用户同时要求修改已有源数据或追加普通字段再导出时，读取[源编辑前置](references/source-edit.md)，复用共享的按键 CLI；纯导出不读取工作簿或建立源编辑计划。

## 直接执行

- 用户说“导出 Tables”“生成 Table 数据和类型”且未限定范围时，`scope=all`，`projectId` 与 `descriptionIds` 留空，表示导出当前有效 TableSettings 中全部已启用描述。
- 用户明确只导出代码或数据时，分别使用 `scope=code` 或 `scope=data`。
- 用户指定 Project 或 Description 时原样传入，不猜测 ID。
- “改某行数值再导出”先按实际源的唯一 rowKey/字段和当前 hash 完成受控源编辑，再对已确认描述 `scope=data`；只改生成 JSON 不算完成。
- “追加普通字段再导出”先用同 CLI 的 `layout=table-fields` 冻结自描述 Schema、原行键与显式默认/逐行值，源 Verify 后对已确认描述导出代码和数据（`scope=all`）；不新建 Skill 或 Action。
- 调用固定链路：`describe -> plan -> execute -> verify`。写入前展示 Plan 的实际输出范围；确认后执行。
- 不退化为任意 C#、反射或临时脚本。Action 未注册、未开放或无法唯一解析 TableSettings 时返回 `blocked`。

## 来源冻结与写入范围

导出时自动读取合同中的 `tableExportResolvedConfigurationInputsAndWriteSet`，复用当前 live Plan；项目可读事实由 Agent 取得。每个 Project 的正式 `ConfigPath` 独立透传原 Exporter，Target 和生成命名空间由自身 `luban.conf` 决定，不要求 `ConfigMaster` / `ConfigRuntime`，也不改写 `topModule`。Plan 冻结配置、完整直接读根和实际解析模板的文件存在性、目录成员与 SHA；组合 `protobuf3 + cs-newtonsoft-json` 的代码分支还冻结隐含模板。Plan 还冻结实际 Luban 工具目录的全部非 `.meta` 成员；`code` 含 `protobuf3` 时冻结实际 protoc 文件和 include 目录，工具缺失 `blocked`。系统 dotnet Host 未冻结，不据此宣称完整执行环境闭包。详细读根规则按需读取 [Table Exporter](../../../Docs/Editor/EditorUtil/EditorUtil.Table/EditorUtil.Table.Exporter.md)，不在 Skill 建立第二份目录清单。

只确认 live Plan 的完整 `writeSet`，包括正式输出 `/**` 及列出的新增输出/祖先目录 `.meta`；配置、Schema、数据源和模板保持只读。读根与正式输出重叠时返回 `blocked`。Execute 前后来源身份、文件或成员漂移时停止，不沿用旧计划、不重放已开始的导出。快照采用保守完整直接读根，可能包含邻近未选输入；Schema 读根外的二次外部 include 不由现模型展开，需独立核对，不能宣称完整依赖闭包已验。当前宿主防护以 live 契约和已发布实现为准。

## 完成标准

新 `receiptVersion=2` 的 `verify` 必须绑定当前场景、组件和 `settingsHash`，从当前 TableSettings 重建限定描述的设置/Job，并确认来源快照、全部计划内 code/data 输出及摘要一致；显式新字段 null、错误版本或缺损不能降为旧摘要。缺少新字段或发生漂移仍为 `partial`，不重放导出。每个重建 Job 的 code/data 槽位须恰好对应一个 `directory` 产物，kind 和解析后的实际物理路径须匹配该 Job 的正式输出；不能用另一目录的同 SHA 或 file 摘要替代。旧 Receipt 保留原摘要验证结果及边界警告；旧摘要 `success` 不能当成新来源冻结或本次完整导出验收。包含代码时，等待本次 Unity 编译与重载，确认无编译错误，并按自身配置核生成类型全名及既有消费者一致。这里只验收导出，不强制进入 Play Mode，也不把它说成完整的 Table 运行时接入成功。
