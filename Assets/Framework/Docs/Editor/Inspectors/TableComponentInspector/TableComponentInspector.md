# TableComponentInspector

Table Inspector 负责管理器选择、正式 Luban Project/Profile、Runtime Bindings、导出入口和运行时表诊断。

## 配置区

Inspector 直接绘制：

- `luban.conf` 路径与 target；
- 任意数量的 Profile；
- 每个 Profile 的 `Enabled`、代码目标、数据目标、输出目录、tag、variant、模板目录和扩展参数；
- 任意数量的 Runtime Binding 类型与数据资源前缀。

配置文件、schema 与 Excel/CSV 由项目维护。`Enabled` 只控制无参数批量导出，可同时选择多个 Profile；Runtime Bindings 独立决定 Player 加载哪些生成 Tables。

## 导出按钮

- `导出代码`
- `导出数据`
- `导出代码和数据`

按钮处理全部 `Enabled` Profile。需要精确选择 Profile 时，可从工具代码或 Pipify 扩展调用带 `profileIds` 的 Exporter 重载。

## 运行时诊断

Play Mode 下显示 `ITableManager.Count` 和加载状态。诊断面板只读取状态，不推进加载流程。

## 关联文档

- [TableSettings.md](../../../Runtime/Modules/Table/Definitions/TableSettings.md)
- [TableManager.md](../../../Runtime/Modules/Table/TableManager.md)
- [EditorUtil.Table.Exporter.md](../../EditorUtil/EditorUtil.Table/EditorUtil.Table.Exporter.md)
