# AssetCacheMenuItems

提供 `Nova/Clean Hotfix Caches` 快捷项，调用 `EditorUtil.Asset.Cache.ClearAllHotfixResources()`；点击后直接执行清理，不显示执行前确认框。

菜单 priority 为 `1031`，位于 `Open Folder`（父菜单取首个子项 `1021`）与 `Enable Logs`（从 `1042` 开始）之间；它与 `Open Folder` 同组，该组上下均显示分隔线。

Play Mode 或正在切换 Play Mode 时菜单禁用。清理范围与安全边界见 [EditorUtil.Asset.Cache](../EditorUtil/EditorUtil.Asset/EditorUtil.Asset.Cache.md)。
