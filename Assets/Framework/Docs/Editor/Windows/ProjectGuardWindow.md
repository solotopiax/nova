# ProjectGuardWindow

窗口标题：`Nova · 项目检查`。菜单入口保持为：`Nova/Open ProjectGuard`。

首行左侧显示“项目检查”，右侧提供两个只读检查按钮：

- 「检查当前场景」：检查当前正在编辑的场景，以及该场景所在目录下的资源。
- 「检查构建场景」：检查 Build Settings 中已启用的场景，以及这些场景所在目录下的资源；不会实际构建项目。

结果区先用 `(1)`、`(2)` 说明检查范围和问题数量。每项结果固定展示：`(1) 状态和规则编号`、`(2) 问题是什么`、`(3) 怎么处理`，以及场景或资源问题的 `(4) 位置`。配置问题不展示导出物路径、设计态来源或导出坐标；完整技术诊断只会在用户主动点击检查按钮后输出到 Console / Editor.log。

窗口只展示 `EditorUtil.ProjectGuard` 报告；它不扫描额外范围、不持有规则、不修改 Scene、Build Settings、Collector 或资源，也不会自动触发或阻断 Unity Build。

规则与 profile 见 [EditorUtil.ProjectGuard](../EditorUtil/EditorUtil.ProjectGuard.md)。
