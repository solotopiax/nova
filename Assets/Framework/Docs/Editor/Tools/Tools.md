# Editor — Tools

**目录**：`Assets/Framework/Scripts/Editor/Tools/`
**命名空间**：`NovaFramework.Editor`

编辑器侧独立工具集合。当前这里主要保留部署脚本与少量旧归类兼容页，不再承载 AB 构建主实现或 Window 主入口。

---

## 子模块

| 目录 | 类 | 说明 |
|------|----|------|
| `Deploys/` | — | Python 部署脚本（`install_apk.py`、`install_aab.py`） |
| [`AssetBundles/`](AssetBundles/AssetBundleBuildProcessor.md) | — | 旧归类兼容页；当前 AB 构建主入口已迁到 `EditorUtil.BundleBuilder` |

## 归类说明

- `PlugPalsWindow` 的代码实际位于 `Assets/Framework/Scripts/Editor/Windows/PlugPalsWindow/`，事实文档见 [../Windows/PlugPalsWindow.md](../Windows/PlugPalsWindow.md)。
- `EditorUtil.PlugPals` 才是 PlugPals 的工具层，见 [../EditorUtil/EditorUtil.PlugPals/EditorUtil.PlugPals.md](../EditorUtil/EditorUtil.PlugPals/EditorUtil.PlugPals.md)。
- `AssetBundleBuildProcessor` 已不在当前源码中；AB 构建事实入口见 [../EditorUtil/EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.md](../EditorUtil/EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.md)。

---

## 关联文档

- [Editor.md](../Editor.md)
- [Menus/Menus.md](../Menus/Menus.md)
