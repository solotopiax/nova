# 修改表源后导出

纯导出直接调用 `nova.project.table.export`。只有用户要求“改 Excel/CSV 数据或为已有自描述 xlsx 追加普通字段再导出”时，读取共享的[按行键编辑表源](../../nova-project-integrate-table/references/source-edit.md)，使用随包 `Agents/Tools/nova_table_source.py` 的 inspect/plan/execute/verify；不新增导出算法或按表名建立 Skill。

从当前有效 TableSettings、luban.conf 和 Schema 确认实际源文件/Sheet、rowKey、字段类型、原值、整文件摘要及所有正式输出目录。源编辑授权与导出输出范围分别冻结，已有授权继续适用。CLI 支持已有 xlsx/csv 标量，以及共享 source-edit 中的 `layout=table-fields` 普通字段追加；结构分支冻结 SchemaHash、唯一自描述映射、原行键、字段类型/注释及明示 default 或全键 values。不能修改外部 Schema/行键/公式、xls/宏或复杂字段，生成物不作为源。

源 Verify 成功后，对同一已确认 Project/Description 执行 Table Action `describe → plan → execute → verify`；仅数据修改用 scope=data；追加普通字段必须对已确认描述 scope=all，同时生成类型和数据，目标不明确不扩大到其他描述。正式导出失败保留源变更和错误，不手工修 JSON/访问类型、不自动恢复其他表。

源 Verify 后才生成新的导出 Plan，不复用源变更前的来源快照。正式导出从 live Plan 自动读取各独立 `ConfigPath`、完整直接读根/实际模板和 code/data 写集，不要求激活 Master。源码/配置/模板或目录成员变化必须重新 Plan；仅运行旧 Receipt 的摘要验证即使 success 也不证明新的来源冻结。新 `receiptVersion=2` 绑定当前场景/组件/settingsHash，从当前 TableSettings 重建限定描述设置/Job，再核 inputs 与全部正式产物；显式新字段缺损/null/错误版本不降为旧摘要，缺失或漂移保持 `partial`；二次外部 Schema include 不在现直接读根模型的完整承诺内。

本 Skill 的 success 要求源编辑与正式导出分别验证；包含代码输出时再等 Unity 编译。用户还要求游戏中读到新值时进入 `nova-project-integrate-table` 的 Play 验收，单次导出不伪造运行证据。清理本轮源编辑请求/Plan/Receipt与临时文件，保留正式源和用户要求的产物。

新导出 Plan 同时冻结实际 Luban 工具目录；代码含 protobuf3 时冻结实际 protoc 文件和 include 目录，缺工具阻断。新 Receipt 的每个 code/data Job 必须恰好一个正确 kind 的 directory 产物，解析后的物理路径必须是该正式输出；同摘要的其他路径或 file 摘要不合格。旧收据保持原摘要语义与边界警告；系统 dotnet Host 和读根外二次外部 Schema include 仍需独立核对，不把工具快照说成环境闭包。
