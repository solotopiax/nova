# TableComponentInspector

Table Inspector 管理多个 Luban 工程、导出描述、Luban 加载、批量导出和运行时诊断。

## Luban 工程

Inspector 支持：

- 新建最小 Luban 工程；
- 添加已有 `luban.conf`；
- 通过“复制”深复制工程目录与 Nova 导出描述；
- 通过“删除”选择仅删除工程引用，或同时从磁盘删除当前配置文件及其明确引用的 Schema 文件；
- 打开 `luban.conf` 或其所在文件夹。

工程 Foldout、配置输入框与下方小字使用统一状态色：配置有效为绿色，配置不存在为红色，多个工程引用同一路径为黄色。“新建”“添加”“复制”“删除”“新增”等操作固定显示在各自所属 Foldout 标题右侧，标题的其余区域用于展开或收起。所有 Foldout 子内容与子按钮按一个汉字宽度逐级缩进。配置文件不存在时保留工程引用并显示红色状态，由用户明确执行“删除”。删除确认框提供“仅删除引用”“从磁盘删除”“取消”；磁盘删除只处理当前配置路径和 `schemaFiles` 明确列出的文件，不删除 Excel、不删除目录，也不递归扫描或删除子目录中的其他配置与 Schema 文件。Assets 内文件移到废纸篓，外部文件执行单文件删除。

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

右向箭头和右侧导出文件路径与左侧输入框保持同一行垂直居中。路径来自导出描述的“数据输出目录”：框架扫描磁盘中已经存在的实际导出文件，再以工程根为唯一基准转换为 Unity AssetPath。Table 只保存以 `Assets/` 开头、使用正斜杠的工程相对路径，不会把 Windows 盘符路径写入 `AssetPath`。Binding 声明的每个 `DataFile` 始终保留一行；文件暂时缺失、位于工程外或转换失败时仅将该行 `AssetPath` 留空并显示“未解析”，不会删除配置项。Inspector 启用时会同步迁移旧配置：工程内绝对路径转为 `Assets/...`，其他无效路径清空，但同样不会删除对应数据项。左侧 Asset 地址才会继续按 `AssetComponent` 的默认资源包执行 YooAsset 收集规则，通过实际 AssetPath 得到最终 Address。数据导出成功或切换 Luban 工程、导出描述、运行时数据 Target 时自动刷新；用户手工修改后的地址会在后续批量导出刷新中保留。Inspector 不提供手动刷新按钮或 YooAsset Package 选择器。

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
