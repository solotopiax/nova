# EditorUtil.Config.DimensionalResolver

**类签名**：`public static class DimensionalResolver`（嵌套于 `EditorUtil.Config`）
**命名空间**：`NovaFramework.Editor`

顶层类维度取数器；按当前 `Platform × Channel × DevelopMode` 坐标，从 `ConfigMasterSO` 的顶层掩码与 Override 列表中解析出最终生效值。本类纯只读，不修改任何数据，可同时服务于 ConfigWindow 绘制前取数与 Exporter 导出取数。

覆盖范围：Namespace（单 string）、HybridCLR（四字段，Editor-only）、YooAsset（两路径，Editor-only）。矩阵类（Common / SDK / Kit）不经本类，走 `PlatformChannelEntry` 直接取数。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionalResolver.cs` | `EditorUtil.Config.DimensionalResolver` | 顶层类维度取数器（含嵌套结果类 `HybridCLRResult` / `YooAssetResult`） |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── EditorUtil.Config (public static partial class)
        └── DimensionalResolver (public static class)
              ├── HybridCLRResult (public sealed class)  — 四字段聚合结果
              └── YooAssetResult  (public sealed class)  — 两路径聚合结果
```

---

## §4 关键字段表

静态工具类，无字段。

### 嵌套结果类字段

**HybridCLRResult**（仅 `#if UNITY_EDITOR`）：

| 字段 | 类型 | 说明 |
|------|------|------|
| `AotMetadataDlls` | `List<DllMasterAssetEntry>` | AOT 元数据 DLL 列表深拷贝；禁共享引用 |
| `GameDlls` | `List<DllMasterAssetEntry>` | 业务 DLL 列表深拷贝；禁共享引用 |
| `LinkXmlTargetPath` | `string` | link.xml 目标路径 |
| `GameEntranceProcedureName` | `string` | 业务入口 Procedure 相对类型名 |

**YooAssetResult**（仅 `#if UNITY_EDITOR`）：

| 字段 | 类型 | 说明 |
|------|------|------|
| `YooAssetSettingsPath` | `string` | YooAssetSettings.asset 项目根相对路径 |
| `BundleCollectorSettingPath` | `string` | BundleCollectorSetting.asset 项目根相对路径 |

---

## §5 完整公开 API

```csharp
// 解析 Namespace 最终生效值
// IsGlobal → master.Namespace；否则遍历 NamespaceOverrides 找首个匹配条目取 Value，无命中回落 master.Namespace
// master 为 null → 返回 string.Empty
public static string ResolveNamespace(
    ConfigMasterSO master,
    PlatformType curP,
    ChannelType curC,
    DevelopMode curM);

// 解析 HybridCLR 面板四字段最终生效值（仅 #if UNITY_EDITOR）
// IsGlobal → 取顶层各默认字段；否则遍历 HybridCLROverrides 找首个匹配条目
// Override 条目的 AotMetadataDlls/GameDlls 为空时回落顶层字段
// AotMetadataDlls / GameDlls 返回深拷贝列表；master 为 null → 各字段返回空值
public static HybridCLRResult ResolveHybridCLR(
    ConfigMasterSO master,
    PlatformType curP,
    ChannelType curC,
    DevelopMode curM);

// 解析 YooAsset 面板两路径最终生效值（仅 #if UNITY_EDITOR）
// IsGlobal → 取顶层默认字段；否则遍历 YooAssetOverrides 找首个匹配条目
// Override 条目路径为 null 或空时回落顶层字段；master 为 null → 两字段返回空字符串
public static YooAssetResult ResolveYooAsset(
    ConfigMasterSO master,
    PlatformType curP,
    ChannelType curC,
    DevelopMode curM);
```

---

## §9 关键算法

### MatchesMask — 坐标匹配

```csharp
// 勾选轴要求分量严格相等，未勾选轴直接跳过（无论 Override 存的是什么值）
private static bool MatchesMask(
    PanelDimensionMask mask,
    PlatformType entryP, ChannelType entryC, DevelopMode entryM,
    PlatformType targetP, ChannelType targetC, DevelopMode targetM);
```

等价逻辑：

```
if mask.ByPlatform  && entryP != targetP → false
if mask.ByChannel   && entryC != targetC → false
if mask.ByDevelopMode && entryM != targetM → false
→ true
```

与 `DimensionProjector.ClipCoordToMask` 的存值规则对称：存入时未勾选轴填 `None`，`MatchesMask` 匹配时未勾选轴直接跳过，两者行为一致。

### 回落链路

```
IsGlobal(mask)?
  Yes → 直接返回顶层字段值（跳过 Override 列表）
  No  → 遍历 XxxOverrides
          首个 MatchesMask 命中 → 取 Override 值（子字段空时回落顶层）
          无命中 → 返回顶层字段值
```

---

## §10 常见误区

| 误区 | 正确理解 |
|------|---------|
| 认为 `ResolveNamespace` 仅 Editor 可用 | `ResolveNamespace` 无 `#if UNITY_EDITOR` 保护，运行时程序集也可用（`NamespaceOverride` 类无 `#if` 包裹），但实际在 Exporter（Editor-only）和 ConfigWindow 中调用 |
| 认为 Override 为空 List 等于"全局唯一" | 不是。IsGlobal 由 `mask.ByPlatform == false && mask.ByChannel == false && mask.ByDevelopMode == false` 决定；Override 列表为空只代表"尚无具体值"，取数仍回落顶层字段 |
| 修改 `HybridCLRResult.AotMetadataDlls` 影响 master | 结果类中的 List 是深拷贝（`CloneDllList`），修改结果不影响 `master` |

---

## §11 使用示例

```csharp
// Exporter.Export 中（D6.2 取数链路）
string ns = DimensionalResolver.ResolveNamespace(
    master, platform, channel, mode);
target.Namespace = ns;

DimensionalResolver.HybridCLRResult hybridCLR =
    DimensionalResolver.ResolveHybridCLR(master, platform, channel, mode);
target.GameEntranceProcedureName = hybridCLR.GameEntranceProcedureName;
target.AotMetadataDlls = hybridCLR.AotMetadataDlls
    .Select(e => new DllAssetEntry(e.AssetLocation)).ToList();

// ConfigWindow.RightPanel.YooAsset.cs 的 ReInjectYooAsset
DimensionalResolver.YooAssetResult result =
    DimensionalResolver.ResolveYooAsset(master, curP, curC, curM);
YooAssetInjector.InjectDirectly(result.YooAssetSettingsPath);
```

---

## §13 关联文档

- [PanelDimensionMask.md](../../../Runtime/Modules/Config/Definitions/PanelDimensionMask.md)（掩码类型）
- [NamespaceOverride.md](../../../Runtime/Modules/Config/Definitions/NamespaceOverride.md)（`NamespaceOverrides` 元素类型）
- [HybridCLROverride.md](../../../Runtime/Modules/Config/Definitions/HybridCLROverride.md)（`HybridCLROverrides` 元素类型）
- [YooAssetOverride.md](../../../Runtime/Modules/Config/Definitions/YooAssetOverride.md)（`YooAssetOverrides` 元素类型）
- [EditorUtil.Config.DimensionProjector.md](EditorUtil.Config.DimensionProjector.md)（写端对称类；存值规则须与本类 MatchesMask 对称）
- [ConfigMasterSO.md](../../../Runtime/Modules/Config/ConfigMasterSO.md)（数据来源）
- [EditorUtil.Config.Exporter.md](EditorUtil.Config.Exporter.md)（调用方，D6.2 导出取数）
- [EditorUtil.Config.YooAssetInjector.md](EditorUtil.Config.YooAssetInjector.md)（消费 `ResolveYooAsset`）
