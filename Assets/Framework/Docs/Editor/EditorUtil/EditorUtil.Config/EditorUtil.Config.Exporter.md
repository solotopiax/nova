# EditorUtil.Config.Exporter

**类签名**：`public static class Exporter`（嵌套于 `EditorUtil.Config`）
**命名空间**：`NovaFramework.Editor`

将 `ConfigMasterSO` 指定 Platform×Channel×DevelopMode 三维组合导出为独立 `ConfigRuntimeSO.asset` 的工具入口。所有文件系统操作使用 `System.IO.Path` 完全限定名（避免与 `NovaFramework.Runtime.Path` 歧义）。

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

`Export` 保留显式 `platform` 参数，作为公共底层 API 可导出调用方明确传入的任意合法矩阵坐标，不在此层读取或改写 Unity BuildTarget。生产入口的约束在调用方执行：ConfigWindow、Pipify `export.config` 与 `nova.project.config.export-runtime` 只会传入 Unity 当前 Active BuildTarget 映射的平台，并在未映射或漂移时阻断。不要把这一层显式 API 的兼容语义误认为 ConfigWindow 可以手工切 Platform。

---

## §9 关键算法

### Export 流程

```
Export(master, platform, channel, mode, savePath):
  1. master == null → return null
  2. master.TryGetEntry(platform, channel, out entry) 失败 → return null
  3. 按目标坐标 Resolve YooAsset 配置；若配置了 `YooAssetSettingsPath`，使用显式占位符上下文解析 `YooFolderName` / `PackageFilePrefix`，单向写入对应 `YooAssetSettings.asset`
  4. System.IO.Path.GetDirectoryName(savePath) → 目录不存在则递归创建
  4. AssetDatabase.LoadAssetAtPath<ConfigRuntimeSO>(savePath)：
     - 存在 → existing（覆盖写，保留已有资产引用）
     - 不存在 → ScriptableObject.CreateInstance<ConfigRuntimeSO>()
  5. target.DevelopMode     = mode
  6. target.Namespace       = DimensionalResolver.ResolveNamespace(master, platform, channel, mode)
                               // D6.2：经 NamespaceMask + NamespaceOverrides 解析最终值（全不勾时 = master.Namespace）
  7. target.AppConfigs          = CloneAppConfigs(master.GetAppConfigs(platform, channel, mode))   深拷贝
  8. target.Platform        = platform
  9. target.Channel         = channel
  10. target.EnabledSDKConfigs = FilterEnabled(entry, mode, master.EnabledSDKs)
  11. target.EnabledKitConfigs = FilterEnabledKits(entry, mode, master.EnabledKits)
  12. DimensionalResolver.HybridCLRResult hybridCLR = DimensionalResolver.ResolveHybridCLR(master, platform, channel, mode)
       // 解析 AOT、Startup、Running 三个 Editor 列表；导出前校验 Startup/Running 不重复
  13. target.GameEntranceProcedureName = hybridCLR.GameEntranceProcedureName
  14. target.AotMetadataDlls = hybridCLR.AotMetadataDlls.Select(e => new DllAssetEntry(e.AssetLocation)).ToList()
  15. target.StartupGameDlls = hybridCLR.StartupGameDlls.Select(e => new DllAssetEntry(e.AssetLocation)).ToList()
       // RunningGameDlls 仅供 Editor 编译/复制/校验，不导出
  15. target.Custom = CloneCustomConfig(master.Custom)
  16. existing == null → CreateAsset；否则 SetDirty
  17. SaveAssets + Refresh → return target
```

### CloneAppConfigs — 深拷贝 AppConfigs

逐字段拷贝 `AppID / AppAesKey / AppAesIV / CustomConfigCmdName / CustomName`，返回新 `AppConfigs` 实例。`src` 为 null 时直接返回 null。（Namespace 不在 AppConfigs 中，在 Export 流程中单独解析后写入 `target.Namespace`。）

### CloneCustomConfig — 深拷贝本地路径默认值

深拷贝 `ConfigMasterSO.Custom.Entries` 到 Runtime 快照，确保设计态和导出物不共享列表或行对象。

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
// ConfigWindow.OnClickExport 中（CurrentPlatform 实时映射 Unity Active BuildTarget；导出目标路径从 m_Master.ExportTarget 取得）
string assetPath = AssetDatabase.GetAssetPath(m_Master.ExportTarget);
ConfigRuntimeSO runtime = EditorUtil.Config.Exporter.Export(
    m_Master,
    m_Master.CurrentPlatform,
    m_Master.CurrentChannel,
    m_Master.CurrentDevelopMode,
    assetPath);

// Pipify Step export.config 中：Platform 已被同步为 Unity Active BuildTarget 映射值，
// Channel / DevelopMode 来自本次 Step 参数。
ConfigMasterSO master = EditorUtil.Config.WorkspaceActive.Get();
string assetPath = AssetDatabase.GetAssetPath(master.ExportTarget);
ConfigRuntimeSO runtime = EditorUtil.Config.Exporter.Export(
    master, parameters.Platform, parameters.Channel, parameters.DevelopMode, assetPath);
```

---

## §12 注意事项

- 使用 `System.IO.Path.GetDirectoryName`（完全限定名），避免与 `NovaFramework.Runtime.Path` 类歧义
- 覆盖写入策略（existing != null 时仅 SetDirty）能保留其他地方对此资产的已有引用；首次导出才 CreateAsset
- 若业务代码直接调用 `Export` 并显式传入其他平台，调用方负责保证构建、YooAsset 与目标产物的一致性；UI / Pipify / Agent Action 已为日常生产路径收口到 Active BuildTarget。

---

## §13 关联文档

- [ConfigMasterSO.md](../../../Editor/Config/ConfigMasterSO.md)
- [ConfigRuntimeSO.md](../../../Runtime/Modules/Config/ConfigRuntimeSO.md)
- [ConfigWindow.md](../../Windows/ConfigWindow.md)
- [EditorUtil.Config.DimensionalResolver.md](EditorUtil.Config.DimensionalResolver.md)（步骤 6/12-15 经此取数；顶层类 Namespace / HybridCLR 维度化后导出侧零改动结构，通过解析器透明获取维度最终值）
