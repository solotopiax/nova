# CDNEditorConfigsOverride

**类签名**：`#if UNITY_EDITOR [Serializable] public sealed class CDNEditorConfigsOverride`
**命名空间**：`NovaFramework.Editor`

CDN 面板维度 Override 单项（仅 Editor 期消费）；对应 `ConfigMasterSO` 的 `CDNEditorConfigs` 字段。当 `CDNEditorConfigsMask` 勾选维度轴后，列表中与当前维度匹配的首个条目覆盖顶层 `CDNEditorConfigs`；列表为空或无命中时，回落顶层 `CDNEditorConfigs` 作为全局默认值。

> 本类仅在 `#if UNITY_EDITOR` 代码块内定义，运行时程序集中不存在此类型；导出流程（`ConfigRuntimeSO`）零改动，Runtime 侧无感知。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/Config/Definitions/CDNEditorConfigsOverride.cs` | `CDNEditorConfigsOverride` | CDN 面板维度 Override 单项（Editor-only） |

---

## §5 完整公开 API

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Platform` | `PlatformType` | `PlatformType.None` | 平台轴；仅 `CDNEditorConfigsMask.ByPlatform == true` 时参与匹配，否则保持 `PlatformType.None` 哨兵 |
| `Channel` | `ChannelType` | `ChannelType.None` | 渠道轴；仅 `CDNEditorConfigsMask.ByChannel == true` 时参与匹配，否则保持 `ChannelType.None` 哨兵 |
| `DevelopMode` | `DevelopMode` | `DevelopMode.Debug` | 开发模式轴；仅 `CDNEditorConfigsMask.ByDevelopMode == true` 时参与匹配；`DevelopMode` 枚举无 `None` 哨兵，不参与匹配时维持默认值 `DevelopMode.Debug` |
| `Config` | `CDNEditorConfigs` | `null` | CDN 部署配置整套快照；为 `null` 时回落顶层 `ConfigMasterSO.CDNEditorConfigs` |

---

## §12 注意事项

- 整个类包裹在 `#if UNITY_EDITOR` 内，运行时程序集中不存在此类型
- `Config` 为**整套快照**语义：切坐标 = 整套字段一份；`AutoLinkLatestVersion` 与 `AutoLinkLatestAssetCheckVersionFiles` 两个独立开关都随坐标切换。命中坐标条目后空字符串也是明确配置，只有无匹配条目或 `Config == null` 时才回落顶层 `CDNEditorConfigs`
- 加维分裂 / 减维合并 / 广播由 `EditorUtil.Config.DimensionProjector` 处理；业务侧不直接维护 `CDNEditorConfigsOverrides` 列表
- 取数必须经 `DimensionalResolver.ResolveCDNEditorConfigs`，禁止业务侧自行遍历 `CDNEditorConfigsOverrides` 匹配

---

## §11 使用示例

```csharp
// DimensionalResolver.ResolveCDNEditorConfigs 取数（命中条目后整份快照独立生效）
CDNEditorConfigs result = DimensionalResolver.ResolveCDNEditorConfigs(
    master,
    PlatformType.Android,
    ChannelType.Google,
    DevelopMode.Debug);
string endpoint = result.Endpoint;
string localDir = result.LocalDirectory;
```

---

## §13 关联文档

- [PanelDimensionMask.md](PanelDimensionMask.md)（`CDNEditorConfigsMask` 类型）
- [ConfigMasterSO.md](../ConfigMasterSO.md)（`CDNEditorConfigs` / `CDNEditorConfigsMask` / `CDNEditorConfigsOverrides` 字段）
- [CDNEditorConfigs.md](CDNEditorConfigs.md)（`Config` 字段整套快照类型）
- [EditorUtil.Config.DimensionalResolver.md](../../EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionalResolver.md)（`ResolveCDNEditorConfigs` 取数）
- [EditorUtil.Config.DimensionProjector.md](../../EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionProjector.md)（加维分裂 / 减维合并 / 广播）
