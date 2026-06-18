# HybridCLROverride

**类签名**：`#if UNITY_EDITOR [Serializable] public sealed class HybridCLROverride`
**命名空间**：`NovaFramework.Runtime`

HybridCLR 面板全部字段的维度 Override 单项（仅 Editor 期消费）；对应 `ConfigMasterSO` 的 `AotMetadataDlls` / `GameDlls` / `LinkXmlTargetPath` / `GameEntranceProcedureName` 四个字段。当 `HybridCLRMask` 勾选维度轴后，列表中与当前维度匹配的首个条目覆盖上述顶层字段；无命中时回落顶层字段值。

> 本类仅在 `#if UNITY_EDITOR` 代码块内定义；导出流程由 `DimensionalResolver.ResolveHybridCLR` 先解析出最终单值，再由 Exporter 写入 `ConfigRuntimeSO`，Runtime 侧无感知。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Runtime/Modules/Config/Definitions/HybridCLROverride.cs` | `HybridCLROverride` | HybridCLR 面板四字段维度 Override 单项（Editor-only） |

---

## §5 完整公开 API

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Platform` | `PlatformType` | `PlatformType.None` | 平台轴；仅 `HybridCLRMask.ByPlatform == true` 时参与匹配 |
| `Channel` | `ChannelType` | `ChannelType.None` | 渠道轴；仅 `HybridCLRMask.ByChannel == true` 时参与匹配 |
| `DevelopMode` | `DevelopMode` | `DevelopMode.Debug` | 开发模式轴；仅 `HybridCLRMask.ByDevelopMode == true` 时参与匹配 |
| `AotMetadataDlls` | `List<DllMasterAssetEntry>` | `new()` | AOT 元数据 DLL 列表 Override（编辑期三字段视图）；列表为空时回落顶层 `ConfigMasterSO.AotMetadataDlls` |
| `GameDlls` | `List<DllMasterAssetEntry>` | `new()` | 业务 DLL 列表 Override（编辑期三字段视图）；列表为空时回落顶层 `ConfigMasterSO.GameDlls` |
| `LinkXmlTargetPath` | `string` | `null` | link.xml 目标位置 Override（项目根相对路径含文件名）；null 或空时回落顶层字段 |
| `GameEntranceProcedureName` | `string` | `null` | 业务入口 Procedure 相对类型名 Override；null 或空时回落顶层字段 |

---

## §12 注意事项

- 整个类包裹在 `#if UNITY_EDITOR` 内，运行时程序集中不存在此类型
- `AotMetadataDlls` / `GameDlls` 为空列表（`Count == 0`）时视为"未 Override"，回落顶层字段；这与 string 字段判断 `IsNullOrEmpty` 行为一致（见 `DimensionalResolver.ResolveHybridCLR` 源码）
- `DimensionProjector.ApplyHybridCLRResult` 写入时对 `AotMetadataDlls` / `GameDlls` 做深拷贝（`new List<>(source)`），禁止共享引用

---

## §11 使用示例

```csharp
// DimensionalResolver.ResolveHybridCLR 取数
DimensionalResolver.HybridCLRResult result = DimensionalResolver.ResolveHybridCLR(
    master,
    PlatformType.Android,
    ChannelType.Google,
    DevelopMode.Debug);
// result.AotMetadataDlls — 深拷贝列表
// result.GameEntranceProcedureName — 最终生效值
```

---

## §13 关联文档

- [DllMasterAssetEntry.md](DllMasterAssetEntry.md)（`AotMetadataDlls` / `GameDlls` 元素类型）
- [PanelDimensionMask.md](PanelDimensionMask.md)（`HybridCLRMask` 类型）
- [ConfigMasterSO.md](../ConfigMasterSO.md)（`HybridCLRMask` / `HybridCLROverrides` 字段）
- [EditorUtil.Config.DimensionalResolver.md](../../../../Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionalResolver.md)（`ResolveHybridCLR` 取数）
