# EditorUtil.Config.DimensionalResolver

**类签名**：`public static class DimensionalResolver`（嵌套于 `EditorUtil.Config`）
**命名空间**：`NovaFramework.Editor`

顶层类维度取数器；按当前 `Platform × Channel × DevelopMode` 坐标，从 `ConfigMasterSO` 的顶层掩码与 Override 列表中解析出最终生效值。本类纯只读，不修改任何数据，可同时服务于 ConfigWindow 绘制前取数与 Exporter 导出取数。YooAsset 显式路径失效时，会只读检查 ConfigMaster 同目录的同名、同类型资产并返回其实际路径，兼容 UPM Sample 导入目录和业务整体迁移目录。

覆盖范围：Namespace（单 string）、HybridCLR（五字段）、YooAsset（两路径与两个导出模板）、CDN（整份 `CDNEditorConfigs`）。本类编译在仅包含 Editor 平台的 `NovaFramework.Editor` 程序集中；`ResolveNamespace` 也仅 Editor 可用。矩阵类（App / Privacy / SDK / Kit）不经本类，走 `PlatformChannelEntry` 取数。

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
              ├── HybridCLRResult (public sealed class)  — 五字段聚合结果
              └── YooAssetResult  (public sealed class)  — 四字段聚合结果
```

---

## §4 关键字段表

静态工具类，无字段。

### 嵌套结果类字段

**HybridCLRResult**（声明没有局部 `#if`，所属程序集仅 Editor）：

| 字段 | 类型 | 说明 |
|------|------|------|
| `AotMetadataDlls` | `List<DllMasterAssetEntry>` | 独立的 AOT 元数据 DLL 列表；元素为 struct 值拷贝 |
| `StartupGameDlls` | `List<DllMasterAssetEntry>` | 独立的启动时 DLL 列表；元素为 struct 值拷贝 |
| `RunningGameDlls` | `List<DllMasterAssetEntry>` | 独立的运行时 DLL 列表；元素为 struct 值拷贝，仅供 Editor 使用 |
| `LinkXmlTargetPath` | `string` | link.xml 目标路径 |
| `GameEntranceProcedureName` | `string` | 业务入口 Procedure 相对类型名 |

**YooAssetResult**（声明没有局部 `#if`，所属程序集仅 Editor）：

| 字段 | 类型 | 说明 |
|------|------|------|
| `YooAssetSettingsPath` | `string` | YooAssetSettings.asset 项目根相对路径 |
| `BundleCollectorSettingPath` | `string` | BundleCollectorSetting.asset 项目根相对路径 |
| `YooFolderName` | `string` | YooAsset 内置资源目录名称模板 |
| `PackageFilePrefix` | `string` | Package 清单文件前缀模板 |

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

// 解析 HybridCLR 面板五字段最终生效值（仅 #if UNITY_EDITOR）
// IsGlobal → 取顶层各默认字段；否则遍历 HybridEditorConfigsOverrides 找首个匹配条目
// 命中后使用整份 Override，空字符串和空列表均为有效值；无命中才回落顶层字段
// 三个 DLL 列表均返回独立列表（struct 元素值拷贝）；master 为 null → 各字段返回空值
public static HybridCLRResult ResolveHybridCLR(
    ConfigMasterSO master,
    PlatformType curP,
    ChannelType curC,
    DevelopMode curM);

// 解析 YooAsset 面板两路径及两个导出模板的最终生效值（仅 #if UNITY_EDITOR）
// IsGlobal → 取顶层默认字段；否则遍历 YooAssetEditorConfigsOverrides 找首个匹配条目
// 命中后使用整份 Override，空路径是有效值；无命中才回落顶层字段
// 非空显式路径不可加载时，仅回退 ConfigMaster 同目录下同名、同类型的配置资产
// master 为 null → 路径和前缀为空，YooFolderName 回退 yoo
public static YooAssetResult ResolveYooAsset(
    ConfigMasterSO master,
    PlatformType curP,
    ChannelType curC,
    DevelopMode curM);

// 解析 CDN 部署配置（仅 #if UNITY_EDITOR），返回独立实例及独立白名单列表
// 首个匹配 Override 的 Config 非 null → 使用其整份配置，空字符串有效
// 首个匹配 Override 的 Config 为 null → 停止查找并回落顶层；无命中同样回落顶层
// master 或最终顶层 Config 为 null → 返回 new CDNEditorConfigs() 的字段默认值
public static CDNEditorConfigs ResolveCDNEditorConfigs(
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

与 `DimensionProjector.ClipCoordToMask` 的存值规则对称：存入时未勾选的 Platform / Channel 轴填 `None`，未勾选的 DevelopMode 轴填 `default(DevelopMode)`（当前为 `Debug`）；`MatchesMask` 匹配时直接跳过未勾选轴。

### 回落链路

```
IsGlobal(mask)?
  Yes → 直接返回顶层字段值（跳过 Override 列表）
  No  → 遍历 XxxOverrides
          首个 MatchesMask 命中 → 取整份 Override 值（子字段空值有效）
          无命中 → 返回顶层字段值
```

CDN 另有整份配置为空的回落分支：首个匹配 Override 的 `Config == null` 时停止查找并使用顶层配置；顶层也为空才返回默认新实例。

---

## §10 常见误区

| 误区 | 正确理解 |
|------|---------|
| 认为 `ResolveNamespace` 没有局部 `#if` 就能在 Player 使用 | 整体所属的 `NovaFramework.Editor` 程序集仅包含 Editor 平台；Player 消费 Exporter 写入 `ConfigRuntimeSO.Namespace` 的结果 |
| 认为 Overrides 容器为空等于"全局唯一" | 不是。IsGlobal 由 `mask.ByPlatform == false && mask.ByChannel == false && mask.ByDevelopMode == false` 决定；Overrides 无匹配条目时才回落顶层字段 |
| 认为命中 Override 后空字符串或空 DLL 列表仍会继承顶层 | 不会。命中条目代表当前坐标整份独立，空值也是明确配置 |
| 修改 `HybridCLRResult.AotMetadataDlls` 影响 master | `CloneDllList` 返回独立 List；`DllMasterAssetEntry` 为 struct，三个 string 字段随元素值拷贝，修改结果不会改变 master |

---

## §11 使用示例

```csharp
// Exporter.Export 中（D6.2 取数链路）
string ns = DimensionalResolver.ResolveNamespace(
    master, platform, channel, mode);
target.Namespace = ns;

DimensionalResolver.HybridCLRResult hybridCLR =
    DimensionalResolver.ResolveHybridCLR(master, platform, channel, mode);
target.HybridConfigs = new HybridConfigs
{
    GameEntranceProcedureName = hybridCLR.GameEntranceProcedureName,
    AotMetadataDlls = hybridCLR.AotMetadataDlls
        .Select(e => new DllAssetEntry(e.AssetLocation)).ToList(),
    StartupGameDlls = hybridCLR.StartupGameDlls
        .Select(e => new DllAssetEntry(e.AssetLocation)).ToList(),
};

// ConfigWindow.RightPanel.YooAsset.cs 的 ReInjectYooAsset
DimensionalResolver.YooAssetResult result =
    DimensionalResolver.ResolveYooAsset(master, curP, curC, curM);
YooAssetInjector.InjectByPath(result.YooAssetSettingsPath);
```

---

## §13 关联文档

- [PanelDimensionMask.md](../../../Editor/Config/Definitions/PanelDimensionMask.md)（掩码类型）
- [NamespaceOverride.md](../../../Editor/Config/Definitions/NamespaceOverride.md)（`NamespaceOverrides` 元素类型）
- [HybridEditorConfigsOverride.md](../../../Editor/Config/Definitions/HybridEditorConfigsOverride.md)（`HybridEditorConfigsOverrides` 元素类型）
- [YooAssetEditorConfigsOverride.md](../../../Editor/Config/Definitions/YooAssetEditorConfigsOverride.md)（`YooAssetEditorConfigsOverrides` 元素类型）
- [CDNEditorConfigsOverride.md](../../../Editor/Config/Definitions/CDNEditorConfigsOverride.md)（`CDNEditorConfigsOverrides` 元素类型）
- [EditorUtil.Config.DimensionProjector.md](EditorUtil.Config.DimensionProjector.md)（写端对称类；存值规则须与本类 MatchesMask 对称）
- [ConfigMasterSO.md](../../../Editor/Config/ConfigMasterSO.md)（数据来源）
- [EditorUtil.Config.Exporter.md](EditorUtil.Config.Exporter.md)（调用方，D6.2 导出取数）
- [EditorUtil.Config.YooAssetInjector.md](EditorUtil.Config.YooAssetInjector.md)（消费 `ResolveYooAsset`）
