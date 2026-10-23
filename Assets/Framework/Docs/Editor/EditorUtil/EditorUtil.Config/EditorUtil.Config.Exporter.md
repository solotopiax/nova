# EditorUtil.Config.Exporter

ConfigWindow 保存时自动按当前维度归一设计态配置；本导出器先检查维度一致性，不执行归一。公开重载生成资源密钥源码时，生成路径发生变化会写回并保存 ConfigMaster；取 App / Privacy / SDK / Kit 的 getter 也可能在内存补建缺少的结构。

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
// master 为空、Platform/Channel 为 None 或未定义值、未找到对应行时返回 null
// 维度一致性或业务 DLL 边界校验失败时抛出；目标路径已有资产时覆盖写入
public static ConfigRuntimeSO Export(
    ConfigMasterSO master, PlatformType platform, ChannelType channel, DevelopMode mode, string savePath);
```

`Export` 保留显式 `platform` 参数，作为公共底层 API 可导出调用方明确传入的任意合法矩阵坐标，不在此层读取或改写 Unity BuildTarget。生产入口的约束在调用方执行：ConfigWindow 可手工切换编辑平台，但只有编辑平台与 Unity 当前 Active BuildTarget 映射一致时才会调用导出；Pipify `export.config` 与 `nova.project.config.export-runtime` 同样在未映射或漂移时阻断。

ConfigWindow 和 Pipify 使用公开 `Export` 时，同时按传入坐标生成客户端资源密钥源码与 link.xml，并持久化源码路径追踪；ConfigMaster 的当前渠道和开发模式不变。密钥不写入 ConfigRuntime。受控 Agent Action 的 internal 窄写入重载仍只写冻结的 ConfigRuntime/YooAssetSettings，不扩大其写入清单；需单独导出密钥源码。源码变化后必须等待 Unity 编译再构建。

三个重载的保存选择不同：公开五参数重载使用 `DateTime.Now`、`saveOnlyTouchedAssets=false`、`exportResourceKeys=true`；internal 六参数重载使用传入的时间，仍为广域保存，资源密钥默认不导出；核心八参数重载的 `exportResourceKeys` 声明默认值为 `false`。`saveOnlyTouchedAssets=true` 仅控制 Runtime 末端保存与同步导入，不取消前面的 YooAssetSettings 保存或显式启用的资源密钥生成。整个导出过程没有跨文件事务回滚，后段失败可能留下前段已写入的内容。

`PrivacyConfigs` 导出时深拷贝 AES Key/IV、`PrivacyInfoConfig` 与 `InfoPlistUsageDescriptions`；iOS 构建后处理读取此单格快照，不直接读取 ConfigWindow 的工作副本。

`YooAssetSettings` 的 `YooFolderName` 与 `PackageFilePrefix` 解析后均未变化、且资产原本干净时，不重新序列化该文件；有实际字段变化才标脏。资产已有脏状态仍通过 `SaveAssetIfDirty` 保存，每次导出都保留选定实例注入。这样受控存档密钥轮换可以冻结同值配置的完整文件，而不改变 ConfigWindow、Pipify 或已有 Action 的字段写入与保存结果。

---

## §9 关键算法

### Export 流程

```
Export(master, platform, channel, mode, savePath):
  1. master == null，或 Platform / Channel 为 None、未定义值 → return null
  2. ValidateDimensionInvariants 失败 → 抛异常；master.TryGetEntry 失败 → return null
  3. ResolveHybridCLR；ValidateGameDllLists 校验 Startup / Running 边界
  4. exportResourceKeys=true 时生成客户端密钥源码与 link.xml；生成路径变化时保存 Master
  5. ResolveYooAsset；按冻结时间与明确坐标解析模板，写入并注入目标 YooAssetSettings
  6. System.IO.Path.GetDirectoryName(savePath) → 目录不存在则递归创建
  7. AssetDatabase.LoadAssetAtPath<ConfigRuntimeSO>(savePath)：
     - 存在 → existing（覆盖写，保留已有资产引用）
     - 不存在 → ScriptableObject.CreateInstance<ConfigRuntimeSO>()
  8. target.DevelopMode     = mode
  9. target.Namespace       = DimensionalResolver.ResolveNamespace(master, platform, channel, mode)
                               // D6.2：经 NamespaceMask + NamespaceOverrides 解析最终值（全不勾时 = master.Namespace）
  10. target.AppConfigs / target.PrivacyConfigs = 当前坐标的逐字段副本
  11. target.Platform / target.Channel = 明确传入的目标坐标
  12. target.EnabledSDKConfigs / target.EnabledKitConfigs = 当前模式按启用类型筛选的新列表
  13. target.HybridConfigs = new HybridConfigs {
        GameEntranceProcedureName = hybridCLR.GameEntranceProcedureName,
        AotMetadataDlls = hybridCLR.AotMetadataDlls.Select(e => new DllAssetEntry(e.AssetLocation)).ToList(),
        StartupGameDlls = hybridCLR.StartupGameDlls.Select(e => new DllAssetEntry(e.AssetLocation)).ToList()
      }
       // RunningGameDlls 仅供 Editor 编译/复制/校验，不导出
  14. target.Custom = CloneCustomConfigData(master.Custom)
  15. existing == null → CreateAsset；否则 SetDirty
  16. saveOnlyTouchedAssets=true → SaveAssetIfDirty + 同步 ImportAsset + 重新加载
      否则 SaveAssets + Refresh；返回 target
```

### CloneAppConfigs — 深拷贝 AppConfigs

逐字段拷贝 `AppID / AppAesKey / AppAesIV / CustomConfigCmdName / CustomName`，返回新 `AppConfigs` 实例。`src` 为 null 时直接返回 null。（Namespace 不在 AppConfigs 中，在 Export 流程中单独解析后写入 `target.Namespace`。）

### CloneCustomConfigData — 深拷贝本地路径默认值

深拷贝 `ConfigMasterSO.Custom.Entries` 到 Runtime 快照，确保设计态和导出物不共享列表或行对象。

### FilterEnabled — 按 DevelopMode 过滤启用的 SDK 配置

调用 `entry.GetSDKConfigs(mode)` 取对应模式下的 SDK 配置列表，筛选类型全名（`cfg.GetType().FullName`）存在于 `enabledTypeNames` 白名单中的非 null 项，返回新列表；元素保持原配置对象引用，本函数不深拷贝元素。

### FilterEnabledKits — 按坐标过滤启用的 Kit 配置

```csharp
private static List<IKitConfig> FilterEnabledKits(PlatformChannelEntry entry, DevelopMode mode, List<string> enabledTypeNames)
```

调用 `entry.GetKitConfigs(mode)` 取对应格的 Kit 配置列表，筛选类型全名（`cfg.GetType().FullName`）存在于 `enabledTypeNames` 白名单内的非 null 项，返回新列表；元素保持原配置对象引用。`enabledTypeNames` 为 null 时直接返回空列表。对称 `FilterEnabled`（SDK）。

---

## §11 使用示例

```csharp
// ConfigWindow.OnClickExport 中（m_EditingPlatform 已通过 Active BuildTarget 一致性门禁；导出目标路径从 m_Master.ExportTarget 取得）
string assetPath = AssetDatabase.GetAssetPath(m_Master.ExportTarget);
ConfigRuntimeSO runtime = EditorUtil.Config.Exporter.Export(
    m_Master,
    m_EditingPlatform,
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
- 若业务代码直接调用 `Export` 并显式传入其他平台，调用方负责保证构建、YooAsset 与目标产物的一致性；ConfigWindow / Pipify / Agent Action 的日常生产路径均以 Active BuildTarget 做最终门禁。

---

## §13 关联文档

- [ConfigMasterSO.md](../../../Editor/Config/ConfigMasterSO.md)
- [ConfigRuntimeSO.md](../../../Runtime/Modules/Config/ConfigRuntimeSO.md)
- [ConfigWindow.md](../../Windows/ConfigWindow.md)
- [EditorUtil.Config.DimensionalResolver.md](EditorUtil.Config.DimensionalResolver.md)（步骤 6/12-15 经此取数；顶层类 Namespace / HybridCLR 维度化后导出侧零改动结构，通过解析器透明获取维度最终值）

## 选择参数与多产物失败

公开五参 Export 明确开启资源密钥生成；带 placeholderTime 的内部兼容重载省略 exportResourceKeys 时为 false，不能只按同名方法推断副作用。saveOnlyTouchedAssets=true 只限定目标保存方式，不取消 YooAsset 或显式密钥生成/追踪路径写入；false 仍全局 SaveAssets/Refresh。日期时间上下文按本次调用冻结。

导出只把 AOT metadata 和 StartupGameDlls 复制到 Runtime，RunningGameDlls 留 Editor 路径/复制配置。已有 Runtime 资产保持对象引用；多个产物不是统一事务，前一步可能已写后一步再失败，DevelopMode 枚举还需上层独立门禁。YooAsset 两字段同值且干净时不重存，但原本 dirty 的同值资产仍会保存；静态 SetSettings 每次仍注入。
