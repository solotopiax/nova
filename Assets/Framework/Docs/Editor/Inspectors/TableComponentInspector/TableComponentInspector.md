# TableComponentInspector

Table Inspector 管理多个 Luban 工程、导出描述、Luban 加载、批量导出和运行时诊断。

## Luban 工程

Inspector 支持：

- 新建最小 Luban 工程；
- 添加已有 `luban.conf`；
- 通过“复制”深复制工程目录与 Nova 导出描述；
- 通过“删除”选择仅删除工程引用，或同时从磁盘删除当前配置文件及其明确引用的 Schema 文件；
- 打开 `luban.conf` 或其所在文件夹。

工程 Foldout、配置输入框与下方小字使用统一状态色：配置有效显示正常色，配置不存在为红色，多个工程引用同一路径为黄色。“新建”“添加”“复制”“删除”“新增”等操作固定显示在各自所属 Foldout 标题右侧，标题的其余区域用于展开或收起。所有 Foldout 子内容与子按钮按一个汉字宽度逐级缩进。配置文件不存在时保留工程引用并显示红色状态，由用户明确执行“删除”。删除确认框提供“仅删除引用”“从磁盘删除”“取消”；磁盘删除只处理当前配置路径和 `schemaFiles` 明确列出的文件，不删除 Excel、不删除目录，也不递归扫描或删除子目录中的其他配置与 Schema 文件。Assets 内文件移到废纸篓，外部文件执行单文件删除。

Inspector 不编辑 `luban.conf`。“配置文件”下一行以禁用输入框展示 `schemaFiles` 中声明的“Schema 文件”，并提供“打开”和“打开文件夹”；Schema 存在时标签保持正常亮度，缺失时标签和路径输入框统一显红。多个 Schema 文件逐行完整展示。每个工程下会按 `目录 -> Excel -> Sheet -> Luban 表` 展示只读清单，数据来自 `dataDir`、`schemaFiles`、Excel 的真实 Sheet 与 table `input`。每个 Excel Foldout 标题右侧提供“打开”和“打开文件夹”。Inspector 会监听 `dataDir` 及其子目录，Excel 文件修改、新建、删除或重命名后自动刷新清单。

## 导出描述

“导出描述”是可增删的列表。点击“新增”后先从 JSON、Binary、Protobuf Binary、Protobuf JSON、MsgPack 中五选一，再按所选方式创建对应预设；点击条目“删除”后必须在二次确认窗口中确认。每项可自定义名称，每项 Foldout 的展开箭头后、标题前提供无文字 Toggle，决定是否参与批量导出；启用项标题使用正常亮色，未启用项标题置灰。未启用项仍可展开查看，但内部详细字段（包括集合 Foldout 标题）统一置灰且不可编辑；展开区相对父标题文字向右错开约一个汉字。启用后可继续编辑 Target、代码/数据 Targets、输出范围、目录、Tags、字段变体、模板目录和高级参数。

代码输出目录和数据输出目录右侧均提供“选择”按钮。已启用描述中，代码 Targets 非空时代码输出目录必填，数据 Targets 非空时数据输出目录必填；缺失目录会使当前目录行、导出描述条目、“导出描述”分组及所属 Luban 工程条目逐级显红。未启用描述不参与该错误状态传播。

“输出表格范围”提供：

- 全部表格；
- 指定表格：可切换“按 Excel”或“按 Luban 表”视图，最终保存并传递重复的 Luban `-o` 表完整名。

## Luban 加载

Luban 加载中的“工程”选择对应 Luban 工程，并继续选择导出描述和运行时 DataTarget。Binding 类型由 `luban.conf` 的 `topModule + manager` 内部建立，不要求用户理解或填写。点击列表 Foldout 右侧“新建”后，必须先从当前 Luban 工程清单中选择工程，再创建加载项；新项默认关联所选工程的首个导出描述和数据 Target。没有可选工程时不创建空条目。

“运行时数据Target”下方提供“Asset 地址清单”Foldout。展开后每行以 `[1]`、`[2]`……`[N]` 标记顺序；左侧为固定宽度、左对齐的可编辑 Asset 地址，右向箭头和只读导出文件路径紧接输入框右边缘并占用剩余宽度。不展示内部 `output_data_file` 标题。每个 Luban 加载条目 Foldout 右侧提供“删除”按钮，删除前必须在二次确认窗口中确认；该操作只删除加载配置，不删除导出文件或 Asset。

```text
[1] [可编辑 Asset 地址]    →    Assets/.../实际导出文件
```

右向箭头和右侧导出文件路径与左侧输入框保持同一行垂直居中。路径来自导出描述的“数据输出目录”：框架扫描磁盘中已经存在的实际导出文件，再以工程根为唯一基准转换为 Unity AssetPath。Table 只保存以 `Assets/` 开头、使用正斜杠的工程相对路径，不会把 Windows 盘符路径写入 `AssetPath`。Binding 声明的每个 `DataFile` 始终保留一行；文件暂时缺失、位于工程外或转换失败时仅将该行 `AssetPath` 留空并显示“未解析”，不会删除配置项。Inspector 启用时会同步迁移旧配置：工程内绝对路径转为 `Assets/...`，其他无效路径清空，但同样不会删除对应数据项。左侧 Asset 地址才会继续按 `AssetComponent` 的默认资源包执行 YooAsset 收集规则，通过实际 AssetPath 得到最终 Address。数据导出成功或切换 Luban 工程/导出描述时刷新；当前实现若仅改变 DataTarget 而来源 Id 不变，不保证本帧触发刷新；用户手工修改后的地址会在后续批量导出刷新中保留。Inspector 不提供手动刷新按钮或 YooAsset Package 选择器。

## 批量导出

- `导出代码`
- `导出数据`
- `导出代码和数据`

按钮处理所有工程中已启用的导出描述。工具代码也可以用 `ProjectId/DescriptionId` 精确指定描述。
三个按钮位于“Luban 工程”Foldout 内容底部，作为工程导出操作的一部分；按钮下方使用横线与“Luban 加载”区域分隔。

## 运行时诊断

Play Mode 下只读显示 `ITableManager.Count` 和加载状态，不在 Inspector 绘制过程中推进运行时加载。

## 关联文档

- [TableSettings.md](../../../Runtime/Modules/Table/Definitions/TableSettings.md)
- [TableManager.md](../../../Runtime/Modules/Table/TableManager.md)
- [EditorUtil.Table.Exporter.md](../../EditorUtil/EditorUtil.Table/EditorUtil.Table.Exporter.md)

## 编辑、导出与映射的实际边界

新建工程仅在缺 conf 时写最小 conf/schema，再追加 Project 引用；已有 conf 不补缺失 schema，缺 conf 但已有同名 schema 时可能覆盖。添加既有工程先写空引用再打开选择器，取消后才移除该引用；AppendProject 本身不创建默认导出描述。新描述默认禁用，选首 Target、全表范围并应用格式预设，代码/数据目录仍为空；用户切换格式会覆盖 Targets，但保留其它筛选、路径与模板字段。未知格式走 JSON 预设，不代表已完成正式兼容验证。

模型按 Project Id、路径和配置状态缓存，路径比较忽略大小写；watcher 刷新前先清旧监听，只注册实际存在目录，中途失败可能只部分注册。总错误色只汇总缺 conf、缺 schema 或启用描述的必填输出目录；Duplicate、schema 解析错误、不可写/冲突路径和缺 Target 不都由该红色聚合覆盖。只读 Excel/Sheet/Table 模型不是每帧重读全部单元格的保证。

复制先递归文件目录（跳 metadata 与 .DS_Store），再 JSON 克隆描述并分配新 Project/Description Id；输出路径及外部引用不统一重映射，没有总回滚，也没有通用 symlink/目标在源子树/工程外路径保护。删除先显示并再次解析准确 conf/schema 清单；Assets 文件移废纸篓，外部单文件永久删除。中途失败不恢复已删文件，也不验证 schema 是否被其它 Project 共享。Project/导出描述删除不会同步清掉加载引用，后续选择器可能回落到首来源。延迟回调只核 Inspector/target 存活及稳定 Id，不冻结文件内容 hash；当前 GUI 允许范围不能直接当作 Agent 的写入授权合同。

表选择器可能在绘制时对既有数组排序去重，未知表名保留；加载源/Target 的未知值也可能自动写回首项。ResolvedBindingTypeName 只有模型成功解析才更新，失败不统一清旧值。新增加载可指向首个未启用描述或空 DataTarget；自动补加载只在整个加载列表为空时发生，不补全部零散缺项。打开 Inspector 还会修复旧 AssetPath 并 SetDirty，不能称为纯只读。

加载映射优先实例化当前 ILubanTableBinding 并读 DataFiles，构造器/getter 可能有副作用，异常被转换为空清单后可回退扫描整个数据目录；该回退可能包含旧残留或 custom target 的任意非 metadata 文件。来源无法匹配时保留旧映射；重建先清数组再填入，中途失败可能留下部分编辑。preserveManualAddress=true 保留已有非空且不同于自动值的地址，不证明该地址仍能被 Collector 正确收集。空 Assets 不会被缺地址检测当成错误，Binding、数据文件、地址及 Runtime 加载仍须按正式链验收。

RunExport 先 Apply 所有当前配置，导出器返回 true 后才刷新加载映射、Apply 并 SetDirty；false 不更新映射。它没有跨全部描述、文件删除、模型刷新及配置写回的总事务，true 也不等于编译或 Runtime 已加载。运行诊断的 Count 是已加载/直接注册的 Tables 容器数量，不是数据行数。
