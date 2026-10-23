# EditorUtil.Table.Exporter

`EditorUtil.Table.Exporter` 直接消费一个或多个正式 `luban.conf + schema + data`，为全部已启用导出描述建立独立工作区，并通过 `OutputApplier` 发布。

## API

```csharp
public static bool ExportAll(TableSettings settings);
public static bool ExportCode(TableSettings settings);
public static bool ExportData(TableSettings settings);

public static bool ExportAll(TableSettings settings, params string[] descriptionIds);
public static bool ExportCode(TableSettings settings, params string[] descriptionIds);
public static bool ExportData(TableSettings settings, params string[] descriptionIds);
```

无 ID 入口处理全部 Project 中已启用的导出描述。精确调用推荐传 `ProjectId/DescriptionId`；仅传描述 ID 时会匹配所有同名描述。

## Luban 参数

Exporter 结构化传递：

- 重复的 `-c`、`-d` 和指定表格模式下重复的 `-o`；
- `--conf`、`-t`；
- 重复的 `-i`、`-e`、`--variant`、`--customTemplateDir`；
- 任意数量的 `-x name=value`。

五种预设只是创建描述时的默认值。用户可以继续修改 Targets、模板和高级参数，Nova 不维护 Codec 安装白名单，也不限制 Luban 自定义 Target。

自定义模板目录传给 Luban 前会经过 `EditorUtil.Luban.ExportHelper`：Nova 框架模板的逻辑路径会自动解析到当前安装形态的真实物理目录，项目自定义模板路径保持原值。这样同一份 Table 配置可同时用于开发仓和通过 UPM 安装的消费工程。

## 导出流程

1. 解析全部 Project 与目标导出描述，校验 ID、Target、范围和输出目录。
2. 每个描述使用独立 `Library/Nova/TableExport/<guid>` 工作区。
3. Luban 只接收该描述声明的原生参数。
4. 包含 `protobuf3 + cs-newtonsoft-json` 时生成 proto、调用 `protoc`，并追加 Nova Protobuf Tables Binding；其他代码 Targets 仍继续透传。
5. 代码和数据分别发布到描述指定目录。
6. 多个描述共用目录时，只替换本次生成的同名产物，不清理其他文件。
7. 全部任务成功后刷新 AssetDatabase；Inspector 随后可根据实际数据文件刷新加载描述。

导出链不生成 Catalog。单表清单和 Codec 构造方式由生成 Binding 提供。

## Agent 表源编辑与普通字段追加

自然语言数据编辑复用随包 `Agents/Tools/nova_table_source.py`，完整请求与边界见 [Table Skill source-edit](../../../../Agents/Skills/nova-project-integrate-table/references/source-edit.md)。已有标量只导出数据；已有自描述 xlsx 的 `layout=table-fields` 支持追加 `int/long/float/double/bool/string` 普通字段，冻结唯一 `readSchemaFromFile=true` 的 `Sheet@xlsx` Schema 映射、源/Schema SHA、旧列/行键与明确默认或全键逐行值。只填相邻空列，保留旧样式/单元格与非目标 ZIP 原压缩记录；Schema 文件不写入。复杂 Schema、公式/合并风险及不完整键集阻断。

源 CLI 的 inspect/plan/一次 execute/只读 verify 与本 Exporter 分离；请求、Plan、Receipt 与写锁统一使用本地 `Library/Nova/AgentActions/TableSource/`。源 Verify 后必须用已确认描述导出代码和数据并等待编译，用户要求运行可用时还需实际 Play 读键/字段。测试结束恢复权威 Excel 后重新正式导出，不手工维护生成属性。

## 受控 Agent 导出入口

`nova.project.table.export` 的请求仍为 `scope`、可选 `projectId / descriptionIds`，底层六个 Exporter API 和原生 Luban 参数保持不变。Table 的 Target、manager 与命名空间由自身 `luban.conf` 决定，不要求当前 ConfigMaster，也不改写 `topModule`。

新 Plan 使用现有 `TableProjectModelBuilder` 解析直接读根，冻结正式配置、配置父目录、`dataDir`、Schema 文件及其父目录、实际解析的自定义模板和当前 Luban 工具目录。包含 `protobuf3` 的代码任务还冻结实际 `protoc` 文件与 include 目录；同时包含 `cs-newtonsoft-json` 时冻结隐含的 `table-protobuf` 模板。快照记录非 `.meta` 文件的字节 SHA 和目录成员集合；新增、删除或修改源文件、配置、模板或工具，都会让旧计划在 Execute 写入前阻断。读根与正式输出重叠时拒绝该受控请求，避免导出自身改变输入而无法证明来源一致；直接公共 API 保持原行为。

该快照采用保守完整直接读根，可能同时冻结相邻未选输入。非 XML Schema 的 Inspector 诊断不会被当作原生 Luban 导出无效；Schema 内再次引用的外部 include 不由现有 Project 模型展开，系统 dotnet Host 也不在工具文件快照内，因此不声明已覆盖其完整依赖闭包。复杂项目若依赖读根之外的二次文件，仍需独立核对或使用原生导出流程。

Execute 只调用一次已选范围的原 Exporter，并核对执行前后来源快照。新 Receipt 保存场景、组件、设置 Hash、选中配置与输入快照；Verify 从当前 TableSettings 重建限定描述和完整 Job，将每份产物的目录类型与物理路径绑定正式输出，再核对来源集合、SHA 和 code/data 产物摘要。缺失新字段或发生漂移时保持 `partial`，不重放导出；只要出现新字段，即使版本缺失或值为 `null`，也不能降级为旧收据。真正没有新字段的旧 Receipt 继续使用原摘要验证结果，并附警告：它不具有新输入冻结证据。Action 的成功仍只覆盖静态来源与产物，编译、Play 读取和资源绑定由调用 Skill 另行验收。

## 关联文档

- [TableSettings.md](../../../Runtime/Modules/Table/Definitions/TableSettings.md)
- [TableComponentInspector.md](../../Inspectors/TableComponentInspector/TableComponentInspector.md)
- [PipifySteps.md](../EditorUtil.Pipify/PipifySteps.md)

## 校验范围与部分发布

显式非空 ID 选择可包含 disabled 描述；裸 ID 可跨 Project 匹配多项，仍先检查全部 Project 的 conf/描述结构。DescriptionValidator 只核空/重复 ID、include/exclude 互斥与 SelectedTables 清单，配置状态 Valid 只证明文件存在/路径不重复，不证明 JSON、Schema、target 或可导出。TableProjectModelBuilder 对单文件读取错误保留部分诊断，Binding 名字是配置拼接，不是已加载类型；显式删除 Schema 仅返回路径，不检查共享引用或授权。

Job/描述持有可变对象，普通 Exporter 不冻结外部修改；单描述独立 OutputApplier，前面成功描述在后续失败时保留。工作区创建/取租约在 try 外可抛，finally 删除失败可遮原结果，全部成功后的 Refresh 也可抛。QueueDirectoryPublish 只替换本次暂存文件，不删除旧正式文件；Protobuf adapter 递归取文件名扁平复制，同 basename 可覆盖。

Invocation Builder 累加原顺序 token 与重复选项，Target/config 为覆盖字段；WithDescription 不自动包含全部 tag/variant/advanced/template。scope 裁剪复制容器，部分内部列表元素仍共享；额外 -x 可重复 outputCodeDir 等项，最终解析取决于 Luban。ToCommandLine 负责空白/引号/反斜线编码，不验证选项含义、NUL、目录授权或所有操作系统解析一致性。

AssetAddressResolver 用临时 Collector 副本进行收集，不保存正式 Collector；仅移除缺失且与请求无关的项，同地址映射后项覆盖。返回地址不证明 Bundle 已构建或运行可加载。监听目录收集只覆盖当时存在的子目录，不解链接，新增子目录需重注册。
