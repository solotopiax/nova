# HybridEditorConfigsOverride

**类签名**：`#if UNITY_EDITOR [Serializable] public sealed class HybridEditorConfigsOverride`
**命名空间**：`NovaFramework.Editor`

HybridCLR 面板全部字段的维度 Override 单项（仅 Editor 期消费）；对应 `AotMetadataDlls` / `StartupGameDlls` / `RunningGameDlls` / `LinkXmlTargetPath` / `GameEntranceProcedureName` 五个字段。当 `HybridEditorConfigsMask` 勾选维度轴后，列表中与当前维度匹配的首个条目覆盖上述顶层字段；无命中时回落顶层字段值。

> 本类仅在 `#if UNITY_EDITOR` 代码块内定义；导出流程由 `DimensionalResolver.ResolveHybridCLR` 先解析出最终单值，再由 Exporter 写入 `ConfigRuntimeSO`，Runtime 侧无感知。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/Config/Definitions/HybridEditorConfigsOverride.cs` | `HybridEditorConfigsOverride` | HybridCLR 面板四字段维度 Override 单项（Editor-only） |

---

## §5 完整公开 API

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Platform` | `PlatformType` | `PlatformType.None` | 平台轴；仅 `HybridEditorConfigsMask.ByPlatform == true` 时参与匹配 |
| `Channel` | `ChannelType` | `ChannelType.None` | 渠道轴；仅 `HybridEditorConfigsMask.ByChannel == true` 时参与匹配 |
| `DevelopMode` | `DevelopMode` | `DevelopMode.Debug` | 开发模式轴；仅 `HybridEditorConfigsMask.ByDevelopMode == true` 时参与匹配 |
| `AotMetadataDlls` | `List<DllMasterAssetEntry>` | `new()` | AOT 元数据 DLL 列表 Override（编辑期三字段视图）；空列表是当前坐标明确配置的有效值 |
| `StartupGameDlls` | `List<DllMasterAssetEntry>` | `new()` | 启动时 DLL 列表 Override；会导出到 Runtime |
| `RunningGameDlls` | `List<DllMasterAssetEntry>` | `new()` | 运行时 DLL 列表 Override；仅供 Editor 编译、复制和校验 |
| `LinkXmlTargetPath` | `string` | `null` | link.xml 目标位置 Override（项目根相对路径含文件名）；空字符串是当前坐标明确配置的有效值 |
| `GameEntranceProcedureName` | `string` | `null` | 业务入口 Procedure 相对类型名 Override；空字符串是当前坐标明确配置的有效值 |

---

## §12 注意事项

- 整个类包裹在 `#if UNITY_EDITOR` 内，运行时程序集中不存在此类型
- 命中坐标条目后整份 Override 独立生效，空列表与空字符串均不回落顶层；只有没有匹配条目时才使用顶层字段
- `DimensionProjector.ApplyHybridCLRResult` 写入时对三个 DLL 列表做深拷贝（`new List<>(source)`），禁止共享引用

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

- [DllMasterAssetEntry.md](DllMasterAssetEntry.md)（三个 DLL 列表的元素类型）
- [PanelDimensionMask.md](PanelDimensionMask.md)（`HybridEditorConfigsMask` 类型）
- [ConfigMasterSO.md](../ConfigMasterSO.md)（`HybridEditorConfigsMask` / `HybridEditorConfigsOverrides` 字段）
- [EditorUtil.Config.DimensionalResolver.md](../../EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionalResolver.md)（`ResolveHybridCLR` 取数）
