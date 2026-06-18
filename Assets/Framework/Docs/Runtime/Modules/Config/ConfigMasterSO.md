# ConfigMasterSO

**类签名**：`[CreateAssetMenu(...)] public sealed class ConfigMasterSO : ScriptableObject, ISerializationCallbackReceiver`
**命名空间**：`NovaFramework.Runtime`
**菜单路径**：`Nova/Config Master`（文件名默认 `ConfigMaster`）

Nova 全局配置主 SO（设计态数据来源），聚合按 DevelopMode 分组的公共参数、Platform×Channel 矩阵、启用 SDK 列表与编辑态选中状态。由 ConfigWindow 加载并编辑，导出生成 ConfigRuntimeSO。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Runtime/Modules/Config/ConfigMasterSO.cs` | `ConfigMasterSO` | ScriptableObject 定义，含全部字段与方法；内嵌 `DevelopModeCommonEntry` |

---

## §3 继承关系

```
UnityEngine.ScriptableObject
  └── ConfigMasterSO (public sealed)
        ├── 实现 ISerializationCallbackReceiver
        └── [CreateAssetMenu(menuName = "Nova/Config Master", fileName = "ConfigMaster")]
              └── 内嵌 DevelopModeCommonEntry ([Serializable] public sealed class)
```

---

## §4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Namespace` | `string` | `null` | 全局业务命名空间；全工程唯一，不随 DevelopMode / Platform / Channel 变化 |
| `EnabledSDKs` | `List<string>` | `new()` | 已启用 SDK Plugin 类型全名列表，与左树勾选对应 |
| `EnabledKits` | `List<string>` | `new()` | 已启用的 Kit 配置类型全名白名单（全局）；Kit 配置实例存储在 `PlatformChannelEntry.KitConfigsByMode`（三维矩阵），导出时按本白名单过滤写入 ConfigRuntimeSO.EnabledKitConfigs |
| `CurrentDevelopMode` | `DevelopMode` | `DevelopMode.Debug` | 当前编辑态选中的开发模式，ConfigWindow 切换写入 |
| `CurrentPlatform` | `PlatformType` | `None` | 当前编辑态选中平台，ConfigWindow 下拉切换写入 |
| `CurrentChannel` | `ChannelType` | `None` | 当前编辑态选中渠道，ConfigWindow 下拉切换写入 |
| `ExportTarget` | `ConfigRuntimeSO` | `null` | 导出目标资产引用；ConfigWindow TopBar 的 ObjectField 直接写入此字段，Pipify Step `export.config` 通过 `AssetDatabase.GetAssetPath(master.ExportTarget)` 取路径后调用 Exporter.Export |
| `GameEntranceProcedureName` | `string` | `null` | 业务入口 Procedure 相对类名（不含 namespace），如 `ProcedurePreload`；由 ConfigWindow → **HybridCLR 配置** 面板编辑 |
| `AotMetadataDlls` | `List<DllMasterAssetEntry>` | `new()` | AOT 元数据 DLL 列表（编辑期三字段）；ConfigWindow **HybridCLR 配置** 面板编辑，导出到 ConfigRuntimeSO（单字段 DllAssetEntry）；供 `EditorUtil.HybridCLR.CopyAotDlls` 消费 |
| `GameDlls` | `List<DllMasterAssetEntry>` | `new()` | 业务 DLL 列表（编辑期三字段）；同上面板编辑，导出到 ConfigRuntimeSO（单字段 DllAssetEntry）；供 `EditorUtil.HybridCLR.CopyGameDlls` 消费 |
| `YooAssetSettingsPath` | `string`（`#if UNITY_EDITOR`） | `null` | YooAssetSettings.asset 的项目根相对路径；仅 Editor 期消费；由 ConfigWindow 设置，由 `EditorUtil.Config.YooAssetInjector` 注入到 `YooAssetConfiguration` |
| `BundleCollectorSettingPath` | `string`（`#if UNITY_EDITOR`） | `null` | BundleCollectorSetting.asset 的项目根相对路径；仅 Editor 期消费；替代 `AssetDatabase.FindAssets` 全工程扫描，精确定位收集器配置 |
| `CommonMask` | `PanelDimensionMask` | `new()` | 应用配置（CommonConfig）面板的维度掩码；全不勾 = 全局唯一 |
| `SDKMasks` | `List<TypedDimensionMask>` | `new()` | SDK Plugin 面板维度掩码列表；每个 Plugin 类型一条，由 `GetSDKMask` 惰性补全 |
| `KitMasks` | `List<TypedDimensionMask>` | `new()` | Kit 配置面板维度掩码列表；每个 Kit 类型一条，由 `GetKitMask` 惰性补全 |
| `NamespaceMask` | `PanelDimensionMask` | `new()` | 名字空间配置面板的维度掩码；全不勾时直接用顶层 `Namespace` 字段 |
| `HybridCLRMask` | `PanelDimensionMask`（`#if UNITY_EDITOR`） | `new()` | HybridCLR 配置面板的维度掩码；全不勾时直接用各顶层字段 |
| `YooAssetMask` | `PanelDimensionMask`（`#if UNITY_EDITOR`） | `new()` | YooAsset 配置面板的维度掩码；全不勾时直接用顶层路径字段 |
| `NamespaceOverrides` | `List<NamespaceOverride>` | `new()` | Namespace 维度 Override 列表；`DimensionalResolver.ResolveNamespace` 按掩码勾选轴匹配首条取值，无命中回落顶层 `Namespace` 字段 |
| `HybridCLROverrides` | `List<HybridCLROverride>`（`#if UNITY_EDITOR`） | `new()` | HybridCLR 面板四字段维度 Override 列表；`DimensionalResolver.ResolveHybridCLR` 取数 |
| `YooAssetOverrides` | `List<YooAssetOverride>`（`#if UNITY_EDITOR`） | `new()` | YooAsset 两路径维度 Override 列表；`DimensionalResolver.ResolveYooAsset` 取数 |
| `m_Entries` | `[SerializeField] List<PlatformChannelEntry>` | `new()` | 序列化形态的完整 Platform×Channel 矩阵行列表 |
| `m_Index` | `[NonSerialized] Dictionary<PlatformType, Dictionary<ChannelType, PlatformChannelEntry>>` | `null` | 运行时二级字典索引，由 `OnAfterDeserialize` 重建；按需懒重建 |

---

## §5 完整公开 API

```csharp
// 按 Platform × Channel × DevelopMode 三维获取公共配置；矩阵行不存在时自动补齐，永不返回 null
public CommonConfig GetCommon(PlatformType platform, ChannelType channel, DevelopMode mode);

// 按 SDK Plugin 类型全名取（或惰性创建）对应的维度掩码；
// SDKMasks 中不存在时自动追加默认条目（全不勾）并返回 Mask，永不返回 null
public PanelDimensionMask GetSDKMask(string typeName);

// 按 Kit 类型全名取（或惰性创建）对应的维度掩码；同 GetSDKMask 语义
public PanelDimensionMask GetKitMask(string typeName);

// 编辑期专用：暴露可变 Entries 视图（仅供 StructureGuard 等 Editor 工具调用，运行时禁止使用）
public List<PlatformChannelEntry> EditorEntries { get; }        // → m_Entries

// 编辑期专用：追加矩阵行（仅供 StructureGuard 调用，运行时禁止使用）
public void EditorAddEntry(PlatformChannelEntry entry);

// 编辑期专用：删除指定索引的行（仅供 StructureGuard 调用，运行时禁止使用）
public void EditorRemoveEntryAt(int index);

// 运行时/编辑期共用：按平台渠道查找矩阵行
public bool TryGetEntry(PlatformType platform, ChannelType channel, out PlatformChannelEntry entry);

// 运行时/编辑期共用：获取所有矩阵行只读视图
public IReadOnlyList<PlatformChannelEntry> GetAllEntries();

// ISerializationCallbackReceiver
public void OnBeforeSerialize();
public void OnAfterDeserialize();     // 重建 m_Index
```

> **EditorEntries / EditorAddEntry / EditorRemoveEntryAt 均为 public，但语义上属于编辑期专用接口。** 运行时不应调用这三个成员，仅供 `StructureGuard.SyncEnumGrid` 等 Editor 工具操作矩阵。

### #if UNITY_EDITOR — Editor-only 字段（直接访问，无方法包装）

| 字段 | 消费方 |
|------|--------|
| `YooAssetSettingsPath` | `EditorUtil.Config.YooAssetInjector.Inject(master)` |
| `BundleCollectorSettingPath` | `EditorUtil.Config.YooAssetInjector.LoadBundleCollector(master)` |
| `LinkXmlTargetPath` | `EditorUtil.HybridCLR`（hybridclr.validate_linkxml / hybridclr.generate_linkxml Step） |
| `AotMetadataDlls` | `EditorUtil.HybridCLR.CopyAotDlls`；ConfigWindow **HybridCLR 配置** 面板编辑 |
| `GameDlls` | `EditorUtil.HybridCLR.CopyGameDlls`；同上面板编辑 |
| `EditorEntries` | `StructureGuard.SyncEnumGrid`（只读可变视图） |

---

## §6 生命周期状态机

```
资产加载（AssetDatabase.LoadAssetAtPath / Find）
  └── OnAfterDeserialize()
        └── RebuildIndex()      重建 m_Index 二级字典

TryGetEntry() 调用时：
  m_Index == null ?
    ├─ Yes → RebuildIndex()（懒重建，EditorAddEntry / EditorRemoveEntryAt 执行后置 null）
    └─ No  → 直接查字典

EditorAddEntry(entry) / EditorRemoveEntryAt(index)：
  └── m_Index = null（失效索引，下次 TryGetEntry 重建）
```

---

## §9 关键算法

### RebuildIndex — 二级字典重建

```
RebuildIndex():
  m_Index = new Dictionary<...>()
  for each entry in m_Entries:
    if m_Index 不含 entry.Platform → 新建 inner dict
    inner dict[entry.Channel] = entry
```

时间复杂度：O(n)，n = m_Entries.Count。

---

## §11 使用示例

```csharp
// Editor — 查找工程内的 ConfigMasterSO
ConfigMasterSO master = EditorUtil.Asset.Operator.Find<ConfigMasterSO>();

// 按 Platform × Channel × DevelopMode 三维读取公共配置
CommonConfig common = master.GetCommon(PlatformType.Android, ChannelType.Google, DevelopMode.Debug);
Debug.Log($"Debug AppID: {common.AppID}");

// 查找矩阵行（含 DevelopMode 维度的 SDK 配置）
if (master.TryGetEntry(PlatformType.Android, ChannelType.Google, out var entry))
{
    List<ISDKPluginConfig> sdkConfigs = entry.GetSDKConfigs(DevelopMode.Debug);
    Debug.Log($"Debug 模式 SDK 配置数: {sdkConfigs.Count}");
}

// 三维导出 RuntimeSO；最后一个参数是示例输出路径，按项目实际落盘位置调整
ConfigRuntimeSO runtime = EditorUtil.Config.Exporter.Export(
    master, PlatformType.Android, ChannelType.Google, DevelopMode.Debug,
    "Assets/Config/Runtime_Android_Google_Debug.asset");
```

---

## §12 注意事项

- `m_Index` 为 `[NonSerialized]`，进入 Play Mode 或重新加载域后自动失效；`OnAfterDeserialize` 会自动重建
- `EditorEntries` 直接暴露内部 `List<PlatformChannelEntry>`，修改后须手动调用 `EditorUtility.SetDirty(master)` 或通过 `StructureGuard` 工具方法（已内置 SetDirty）
- `YooAssetSettingsPath` 与 `BundleCollectorSettingPath` 均为**项目根相对路径**（`PAT-36`）；不得写入绝对路径，否则跨机器失效；由 ConfigWindow 负责将 `EditorUtility.OpenFilePanel` 拿到的绝对路径转换后写入

---

## §13 关联文档

- [CommonConfig.md](CommonConfig.md)
- [PlatformChannelEntry.md](PlatformChannelEntry.md)
- [ConfigRuntimeSO.md](ConfigRuntimeSO.md)（导出产物）
- [DllMasterAssetEntry.md](Definitions/DllMasterAssetEntry.md)（AotMetadataDlls / GameDlls 元素类型，编辑期三字段视图）
- [DllAssetEntry.md](Definitions/DllAssetEntry.md)（运行期单字段视图，导出到 ConfigRuntimeSO 的元素类型）
- [EditorUtil.Asset.Operator.md](../../../Editor/EditorUtil/EditorUtil.Asset/EditorUtil.Asset.Operator.md)
- [EditorUtil.Config.StructureGuard.md](../../../Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.StructureGuard.md)
- [EditorUtil.Config.Exporter.md](../../../Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.Exporter.md)
- [EditorUtil.Config.WorkspaceActive.md](../../../Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.WorkspaceActive.md)（Globals.json 激活锚点，ConfigWindow 用于加载 ConfigMaster）
- [EditorUtil.Config.YooAssetInjector.md](../../../Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.YooAssetInjector.md)（消费 YooAssetSettingsPath / BundleCollectorSettingPath）
- [EditorUtil.HybridCLR.md](../../../Editor/EditorUtil/EditorUtil.HybridCLR/EditorUtil.HybridCLR.md)（AotMetadataDlls / GameDlls 消费方）
- [ConfigWindow.md](../../../Editor/Windows/ConfigWindow.md)
- [Definitions/PanelDimensionMask.md](Definitions/PanelDimensionMask.md)（`CommonMask` / `SDKMasks` / `KitMasks` / `NamespaceMask` / `HybridCLRMask` / `YooAssetMask` 字段类型）
- [Definitions/TypedDimensionMask.md](Definitions/TypedDimensionMask.md)（`SDKMasks` / `KitMasks` 列表元素类型）
- [Definitions/NamespaceOverride.md](Definitions/NamespaceOverride.md)（`NamespaceOverrides` 列表元素类型）
- [Definitions/HybridCLROverride.md](Definitions/HybridCLROverride.md)（`HybridCLROverrides` 列表元素类型，Editor-only）
- [Definitions/YooAssetOverride.md](Definitions/YooAssetOverride.md)（`YooAssetOverrides` 列表元素类型，Editor-only）
- [EditorUtil.Config.DimensionProjector.md](../../../Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionProjector.md)（维度投影器，掩码消费方）
- [EditorUtil.Config.DimensionalResolver.md](../../../Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionalResolver.md)（维度取数器，Override 列表消费方）
