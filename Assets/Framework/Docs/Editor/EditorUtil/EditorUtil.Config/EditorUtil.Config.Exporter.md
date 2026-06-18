# EditorUtil.Config.Exporter

**类签名**：`public static class Exporter`（嵌套于 `EditorUtil.Config`）
**命名空间**：`NovaFramework.Editor`

将 `ConfigMasterSO` 当前 Platform×Channel×DevelopMode 三维组合导出为独立 `ConfigRuntimeSO.asset` 的工具入口。所有文件系统操作使用 `System.IO.Path` 完全限定名（避免与 `NovaFramework.Runtime.Path` 歧义）。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.Exporter.cs` | `EditorUtil.Config.Exporter` | 导出工具类 |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── EditorUtil.Config (public static partial class)
        └── Exporter (public static class)
```

---

## §4 关键字段表

无字段（静态工具类）。

---

## §5 完整公开 API

```csharp
// 将指定 Platform×Channel×DevelopMode 三维组合的配置写入目标路径
// master 为 null 或未找到对应行时返回 null；目标路径已有资产时覆盖写入
public static ConfigRuntimeSO Export(
    ConfigMasterSO master, PlatformType platform, ChannelType channel, DevelopMode mode, string savePath);
```

---

## §9 关键算法

### Export 流程

```
Export(master, platform, channel, mode, savePath):
  1. master == null → return null
  2. master.TryGetEntry(platform, channel, out entry) 失败 → return null
  3. System.IO.Path.GetDirectoryName(savePath) → 目录不存在则递归创建
  4. AssetDatabase.LoadAssetAtPath<ConfigRuntimeSO>(savePath)：
     - 存在 → existing（覆盖写，保留已有资产引用）
     - 不存在 → ScriptableObject.CreateInstance<ConfigRuntimeSO>()
  5. target.DevelopMode     = mode
  6. target.Namespace       = DimensionalResolver.ResolveNamespace(master, platform, channel, mode)
                               // D6.2：经 NamespaceMask + NamespaceOverrides 解析最终值（全不勾时 = master.Namespace）
  7. target.Common          = CloneCommon(master.GetCommon(platform, channel, mode))   深拷贝
  8. target.Platform        = platform
  9. target.Channel         = channel
  10. target.EnabledSDKConfigs = FilterEnabled(entry, mode, master.EnabledSDKs)
  11. target.EnabledKitConfigs = FilterEnabledKits(entry, mode, master.EnabledKits)
  12. DimensionalResolver.HybridCLRResult hybridCLR = DimensionalResolver.ResolveHybridCLR(master, platform, channel, mode)
       // D6.2：经 HybridCLRMask + HybridCLROverrides 解析最终四字段值（全不勾时 = 顶层各默认字段）
  13. target.GameEntranceProcedureName = hybridCLR.GameEntranceProcedureName
  14. target.AotMetadataDlls = hybridCLR.AotMetadataDlls.Select(e => new DllAssetEntry(e.AssetLocation)).ToList()
  15. target.GameDlls        = hybridCLR.GameDlls.Select(e => new DllAssetEntry(e.AssetLocation)).ToList()
  15. existing == null → CreateAsset；否则 SetDirty
  16. SaveAssets + Refresh → return target
```

### CloneCommon — 深拷贝 CommonConfig

逐字段拷贝 3 个 string 字段（AppID / AppAesKey / AppAesIV），返回新 `CommonConfig` 实例。`src` 为 null 时直接返回 null。（Namespace 不在 CommonConfig 中，在 Export 流程第 6 步单独从 `master.Namespace` 赋值到 `target.Namespace`。）

### FilterEnabled — 按 DevelopMode 过滤启用的 SDK 配置

调用 `entry.GetSDKConfigs(mode)` 取对应模式下的 SDK 配置列表，筛选类型全名（`cfg.GetType().FullName`）存在于 `enabledTypeNames` 白名单中的非 null 项，返回新列表。

### FilterEnabledKits — 按坐标过滤启用的 Kit 配置

```csharp
private static List<IKitConfig> FilterEnabledKits(PlatformChannelEntry entry, DevelopMode mode, List<string> enabledTypeNames)
```

调用 `entry.GetKitConfigs(mode)` 取对应格的 Kit 配置列表，筛选类型全名（`cfg.GetType().FullName`）存在于 `enabledTypeNames` 白名单内的非 null 项，返回新列表。`enabledTypeNames` 为 null 时直接返回空列表。对称 `FilterEnabled`（SDK）。

---

## §11 使用示例

```csharp
// ConfigWindow.OnClickExport 中（导出目标路径从 m_Master.ExportTarget 取得）
string assetPath = AssetDatabase.GetAssetPath(m_Master.ExportTarget);
ConfigRuntimeSO runtime = EditorUtil.Config.Exporter.Export(
    m_Master,
    m_Master.CurrentPlatform,
    m_Master.CurrentChannel,
    m_Master.CurrentDevelopMode,
    assetPath);

// Pipify Step export.config 中（同一流程）
ConfigMasterSO master = EditorUtil.Asset.Operator.Find<ConfigMasterSO>();
string assetPath = AssetDatabase.GetAssetPath(master.ExportTarget);
ConfigRuntimeSO runtime = EditorUtil.Config.Exporter.Export(
    master, master.CurrentPlatform, master.CurrentChannel, master.CurrentDevelopMode, assetPath);
```

---

## §12 注意事项

- 使用 `System.IO.Path.GetDirectoryName`（完全限定名），避免与 `NovaFramework.Runtime.Path` 类歧义
- 覆盖写入策略（existing != null 时仅 SetDirty）能保留其他地方对此资产的已有引用；首次导出才 CreateAsset

---

## §13 关联文档

- [ConfigMasterSO.md](../../../Runtime/Modules/Config/ConfigMasterSO.md)
- [ConfigRuntimeSO.md](../../../Runtime/Modules/Config/ConfigRuntimeSO.md)
- [ConfigWindow.md](../../Windows/ConfigWindow.md)
- [EditorUtil.Config.DimensionalResolver.md](EditorUtil.Config.DimensionalResolver.md)（步骤 6/12-15 经此取数；顶层类 Namespace / HybridCLR 维度化后导出侧零改动结构，通过解析器透明获取维度最终值）
