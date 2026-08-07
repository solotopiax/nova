# YooAssetEditorConfigsOverride

**类签名**：`#if UNITY_EDITOR [Serializable] public sealed class YooAssetEditorConfigsOverride`
**命名空间**：`NovaFramework.Editor`

YooAsset 四字段的维度 Override 单项（仅 Editor 期消费）；对应两条资产路径及 `YooFolderName` / `PackageFilePrefix` 两个导出模板。当 `YooAssetEditorConfigsMask` 勾选维度轴后，列表中与当前维度匹配的首个条目整份生效；无命中时回落顶层字段值。

> 本类仅在 `#if UNITY_EDITOR` 代码块内定义，运行时（`ConfigRuntimeSO`）零改动，Runtime 侧无感知。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/Config/Definitions/YooAssetEditorConfigsOverride.cs` | `YooAssetEditorConfigsOverride` | YooAsset 两路径维度 Override 单项（Editor-only） |

---

## §5 完整公开 API

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Platform` | `PlatformType` | `PlatformType.None` | 平台轴；仅 `YooAssetEditorConfigsMask.ByPlatform == true` 时参与匹配 |
| `Channel` | `ChannelType` | `ChannelType.None` | 渠道轴；仅 `YooAssetEditorConfigsMask.ByChannel == true` 时参与匹配 |
| `DevelopMode` | `DevelopMode` | `DevelopMode.Debug` | 开发模式轴；仅 `YooAssetEditorConfigsMask.ByDevelopMode == true` 时参与匹配 |
| `YooAssetSettingsPath` | `string` | `null` | `YooAssetSettings.asset` 项目根相对路径 Override；空字符串是当前坐标明确配置的有效值 |
| `BundleCollectorSettingPath` | `string` | `null` | `BundleCollectorSetting.asset` 项目根相对路径 Override；空字符串是当前坐标明确配置的有效值 |
| `YooFolderName` | `string` | `yoo` | 导出时写入 `YooAssetSettings.asset` 的根文件夹名称模板，支持标准占位符 |
| `PackageFilePrefix` | `string` | 空 | 导出时写入 `YooAssetSettings.asset` 的文件名前缀模板，支持标准占位符 |

---

## §12 注意事项

- 整个类包裹在 `#if UNITY_EDITOR` 内，运行时程序集中不存在此类型
- 两字段均为**项目根相对路径**（`PAT-36`），禁止写入绝对路径
- 命中坐标条目后整份 Override 独立生效，空路径不回落顶层；只有没有匹配条目时才使用顶层路径
- YooAsset mask 维度切换后 `ConfigWindow.RightPanel.YooAsset.cs` 的 `ReInjectYooAsset()` 会立即触发 `YooAssetInjector.Inject`，确保编辑期 YooAsset 工具链始终使用正确的 Settings 实例

---

## §11 使用示例

```csharp
// DimensionalResolver.ResolveYooAsset 取数
DimensionalResolver.YooAssetResult result = DimensionalResolver.ResolveYooAsset(
    master,
    PlatformType.Android,
    ChannelType.Google,
    DevelopMode.Debug);
string settingsPath = result.YooAssetSettingsPath;
string collectorPath = result.BundleCollectorSettingPath;
string yooFolderName = result.YooFolderName;
string packageFilePrefix = result.PackageFilePrefix;
```

---

## §13 关联文档

- [PanelDimensionMask.md](PanelDimensionMask.md)（`YooAssetEditorConfigsMask` 类型）
- [ConfigMasterSO.md](../ConfigMasterSO.md)（`YooAssetEditorConfigsMask` / `YooAssetEditorConfigsOverrides` 字段）
- [EditorUtil.Config.DimensionalResolver.md](../../EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionalResolver.md)（`ResolveYooAsset` 取数）
- [EditorUtil.Config.YooAssetInjector.md](../../EditorUtil/EditorUtil.Config/EditorUtil.Config.YooAssetInjector.md)（消费解析结果，注入 YooAssetConfiguration）
