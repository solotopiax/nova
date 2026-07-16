# ProjectGuardWindow

菜单入口：`Nova/Open Project Guard`。

窗口只提供 Quick Validate 与 Build Validate 两个按钮并展示 `EditorUtil.ProjectGuard` 报告。它不扫描额外范围、不持有规则、不修改 Scene、Build Settings、Collector 或资源，也不会自动触发或阻断 Unity Build。

规则与 profile 见 [EditorUtil.ProjectGuard](../EditorUtil/EditorUtil.ProjectGuard.md)。
