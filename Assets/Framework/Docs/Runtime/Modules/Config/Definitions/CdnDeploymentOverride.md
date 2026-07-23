# CdnDeploymentOverride

**类签名**：`#if UNITY_EDITOR [Serializable] public sealed class CdnDeploymentOverride`
**命名空间**：`NovaFramework.Runtime`

CDN 面板维度 Override 单项（仅 Editor 期消费）；对应 `ConfigMasterSO` 的 `CdnDeployment` 字段。当 `CdnMask` 勾选维度轴后，列表中与当前维度匹配的首个条目覆盖顶层 `CdnDeployment`；列表为空或无命中时，回落顶层 `CdnDeployment` 作为全局默认值。

> 本类仅在 `#if UNITY_EDITOR` 代码块内定义，运行时程序集中不存在此类型；导出流程（`ConfigRuntimeSO`）零改动，Runtime 侧无感知。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Runtime/Modules/Config/Definitions/CdnDeploymentOverride.cs` | `CdnDeploymentOverride` | CDN 面板维度 Override 单项（Editor-only） |

---

## §5 完整公开 API

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Platform` | `PlatformType` | `PlatformType.None` | 平台轴；仅 `CdnMask.ByPlatform == true` 时参与匹配，否则保持 `PlatformType.None` 哨兵 |
| `Channel` | `ChannelType` | `ChannelType.None` | 渠道轴；仅 `CdnMask.ByChannel == true` 时参与匹配，否则保持 `ChannelType.None` 哨兵 |
| `DevelopMode` | `DevelopMode` | `DevelopMode.Debug` | 开发模式轴；仅 `CdnMask.ByDevelopMode == true` 时参与匹配；`DevelopMode` 枚举无 `None` 哨兵，不参与匹配时维持默认值 `DevelopMode.Debug` |
| `Config` | `CdnDeploymentConfig` | `null` | CDN 部署配置整套快照（10 字段一份）；为 `null` 时回落顶层 `ConfigMasterSO.CdnDeployment` |

---

## §12 注意事项

- 整个类包裹在 `#if UNITY_EDITOR` 内，运行时程序集中不存在此类型
- `Config` 为**整套快照**语义：切坐标 = 整套 10 字段一份；命中坐标条目后空字符串也是明确配置，只有无匹配条目或 `Config == null` 时才回落顶层 `CdnDeployment`
- 加维分裂 / 减维合并 / 广播由 `EditorUtil.Config.DimensionProjector` 处理；业务侧不直接维护 `CdnOverrides` 列表
- 取数必须经 `DimensionalResolver.ResolveCdn`，禁止业务侧自行遍历 `CdnOverrides` 匹配

---

## §11 使用示例

```csharp
// DimensionalResolver.ResolveCdn 取数（命中条目后整份快照独立生效）
CdnDeploymentConfig result = DimensionalResolver.ResolveCdn(
    master,
    PlatformType.Android,
    ChannelType.Google,
    DevelopMode.Debug);
string endpoint = result.Endpoint;
string localDir = result.LocalDirectory;
```

---

## §13 关联文档

- [PanelDimensionMask.md](PanelDimensionMask.md)（`CdnMask` 类型）
- [ConfigMasterSO.md](../ConfigMasterSO.md)（`CdnDeployment` / `CdnMask` / `CdnOverrides` 字段）
- [CdnDeploymentConfig.md](CdnDeploymentConfig.md)（`Config` 字段整套快照类型）
- [EditorUtil.Config.DimensionalResolver.md](../../../../Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionalResolver.md)（`ResolveCdn` 取数）
- [EditorUtil.Config.DimensionProjector.md](../../../../Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionProjector.md)（加维分裂 / 减维合并 / 广播）
