# EditorUtil.Asset.Cache

Editor 下热更资源缓存的一键清理能力，由 AssetComponent Inspector 与 Nova 顶级菜单共用。

## 清理范围

- YooAsset Editor 沙盒根目录：`<project>/Library/<YooFolderName>`，其中目录名通过 `YooAssetConfiguration.GetYooFolderName()` 动态获取。
- 框架自主保存的可启动版本记录：仅删除 `Application.persistentDataPath/Asset` 第一层的 `*.version` 文件。

以下内容不会删除：

- `asset-check-device-id.dat`，它只记录稳定 DeviceID，不属于热更资源缓存。
- `Application.persistentDataPath/Asset` 下其他扩展名文件。
- StreamingAssets 首包资源与 Bundle 构建产物。

## 安全边界

- Play Mode 中禁止执行。
- 点击入口后直接删除，不显示执行前确认框；完成或失败后显示结果提示。
- `YooFolderName` 为空、沙盒路径退化为 `Library` 根/项目根、逃逸出 `Library` 或落入其他项目保护目录时拒绝清理。
- Windows Editor 的路径边界比较不区分大小写，非 Windows Editor 区分大小写；因此大小写错误的 `library` 不会在非 Windows 平台被接受为 `Library` 子目录。
- 递归删除目标为文件系统根、项目根、`Library` 根或其他项目保护目录时拒绝清理。

## API

```csharp
EditorUtil.Asset.Cache.ClearAllHotfixResources();
string sandboxRoot = EditorUtil.Asset.Cache.GetEditorSandboxRootPath();
```

`ClearAllAtPaths` 是供隔离目录回归测试使用的内部核心方法。

## 入口

- AssetComponent Inspector → 热更配置 → 启动期热更 Tag 提示框下方 → `清空本地热更资源缓存`；按钮下方 HelpBox 说明清理范围与 DeviceID 保留规则
- Unity 菜单 → `Nova/Clean Hotfix Caches`
