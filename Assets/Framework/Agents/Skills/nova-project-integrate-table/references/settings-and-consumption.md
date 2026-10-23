# 表格设置与实际消费绑定

先按当前 Framework 的 `Docs/Runtime/Modules/Table/Definitions/TableSettings.md`、`Docs/Runtime/Modules/Table/TableComponent.md` 核对真实字段和生命周期。当前组件字段为 `m_Setting`；`TableSettings.Projects` 与 `Runtime.LoadDescriptions` 是不同列表。`ProjectId`、`ExportDescriptionId`、`RuntimeDataTarget`、每个 `DataFile` 的 AssetAddress/AssetPath 来自有效配置、Luban 及实际导出，不照抄 Sample 默认 `main` 或路径。

配置写入复用[共享 Unity 保存规则](../../nova-project-edit-settings/references/unity-mcp-editing.md)。普通自有 Prefab 中已有字段优先 `nova.project.settings.inspect-target/patch-target`，请求从 live describe 和当前序列化属性取精确路径、类型和值；复杂列表/聚合字段不能由 Schema 表示时，使用本契约声明的共享工具。先读取完整现有列表、唯一项目/描述 ID 和原值，再保持无关项，只构造确认目标的最终列表。不把数组某项缺失猜为空，也不执行私有 Inspector 方法。

Prefab 使用当前 `manage_prefabs(action="modify_contents")`，`prefab_path` 绑定唯一自有资产，`target` 为唯一完整对象路径，`component_properties` 中使用真实 `NovaFramework.Runtime.TableComponent` 与当前字段结构。场景使用 `manage_components(set_property)` 的当前对象 ID、完整组件类型和精确 `component_index`，随后只保存已冻结的活动场景。Stage、重复组件、变体/嵌套与内部引用遵守共享规则；无无关草稿证明时不全局保存。工具失败可部分写入，先读回，断线不重放。

设置保存后读取 Unity 对象和实际文件/meta，核对完整映射、非目标项、资产来源及唯一写集。既有 Inspector 会从实际导出文件刷新 AssetPath/采集地址，但“看过 Inspector”不等于执行了刷新或生成有效配置；最终每个必需文件都要确实存在并映射到当前默认资源包的地址。结构编辑、创建新的 Luban 工程或生成绑定有额外需求时用已确认的源文件入口和正式导出，不用任意 C# 或 YAML 填空。

业务代码写入仅限用户授权的项目程序集、现有加载 Owner 和读取入口。`TableComponent.Start()` 初始化 Manager，不自动加载；依照实际 Config/Asset 初始化顺序调用公开 `Nova.Table.LoadAsync/LoadSync`，不要另建并行加载 Owner。真实运行后按具体 `HasTable<T>/GetTable<T>` 检查冻结行和字段，Count 非零、IsLoadOver 或 Loader 返回 true 都不保证全部目标表存在。

固定配置保存/导出使用既有工具，开放业务实现由 Agent 在已冻结源码范围完成。结果区分源、设置、导出、编译、Play 与 Player；测试恢复本轮数据、语言和场景状态并清理临时探针，不覆盖原工作。
