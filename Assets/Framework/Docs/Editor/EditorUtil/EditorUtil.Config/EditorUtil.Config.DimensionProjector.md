# EditorUtil.Config.DimensionProjector

**类签名**：`public static class DimensionProjector`（嵌套于 `EditorUtil.Config`）
**命名空间**：`NovaFramework.Editor`

Config 面板维度投影器；按 `PanelDimensionMask` 在 `m_WorkingCopy` 上执行加维分裂 / 减维合并 / 广播三种投影操作，支持矩阵三类（Common / SDK / Kit）与顶层三类（Namespace / HybridCLR / YooAsset）共六种面板。全部操作作用于调用方传入的 `SerializedObject`（m_WorkingCopy），不落盘，不设脏标记，由 ConfigWindow 负责后续持久化。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionProjector.cs` | `EditorUtil.Config.DimensionProjector` | 维度投影器主体（含公开枚举 / 公开操作 / 全部私有辅助） |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── EditorUtil.Config (public static partial class)
        └── DimensionProjector (public static class)
              ├── PanelKind (public enum)          — 六种面板
              ├── DimensionAxis (public enum)       — 三个轴
              └── Coord (public readonly struct)    — 三维坐标值类型
```

---

## §4 关键字段表

静态工具类，无字段。所有状态由调用方的 `ConfigMasterSO` / `SerializedObject` 持有。

---

## §5 完整公开 API

### 嵌套类型

```csharp
// 面板种类：区分六种面板
public enum PanelKind
{
    Common,     // 公共配置（CommonConfig），矩阵类
    SDK,        // SDK Plugin 配置，矩阵类；typeName 有效
    Kit,        // Kit 配置，矩阵类；typeName 有效
    Namespace,  // 顶层 Namespace 字段（NamespaceOverrides 旁路）
    HybridCLR,  // 顶层 HybridCLR 面板字段组（Editor-only，HybridCLROverrides 旁路）
    YooAsset,   // 顶层 YooAsset 两路径（Editor-only，YooAssetOverrides 旁路）
}

// 维度轴：对应 PanelDimensionMask 三个 bool 字段
public enum DimensionAxis
{
    Platform,
    Channel,
    DevelopMode,
}

// 三维坐标值类型（不可变 struct，轻量避免散参）
public readonly struct Coord
{
    public readonly PlatformType Platform;
    public readonly ChannelType Channel;
    public readonly DevelopMode Mode;
    public Coord(PlatformType platform, ChannelType channel, DevelopMode mode);
}
```

### 三个公开操作

```csharp
// 加维分裂：启用指定轴后，将当前坐标格的值深拷贝广播到该轴所有取值的同组格中
// 步骤：读当前格快照 → mask.[axis]=true → 遍历轴向所有坐标 → 逐格写深拷贝
public static void OnDimensionEnabled(
    ConfigMasterSO master,
    SerializedObject masterSO,   // SDK/Kit 路径必需；顶层类/Common 亦需用于 ApplyModifiedProperties
    PanelKind panelKind,
    string typeName,             // SDK 或 Kit 类型全名；Common 时忽略
    Coord curCoord,
    DimensionAxis axis);

// 减维合并：禁用指定轴后，将当前坐标格的值深拷贝广播到新 mask 下所有同组格（其余格数据丢弃）
// 步骤：读当前格快照 → mask.[axis]=false → GroupMembers(新 mask) → 全体写深拷贝
// 若 IsGlobal（全不勾）：顶层类将值回写顶层默认字段（如 master.Namespace = snapshot）
public static void OnDimensionDisabled(
    ConfigMasterSO master,
    SerializedObject masterSO,
    PanelKind panelKind,
    string typeName,
    Coord curCoord,
    DimensionAxis axis);

// 广播：将当前坐标格的值深拷贝广播到同组其余格；ConfigWindow 侦测字段变更后调用
public static void BroadcastWithinGroup(
    ConfigMasterSO master,
    SerializedObject masterSO,
    PanelKind panelKind,
    string typeName,
    Coord curCoord);
```

---

## §9 关键算法

### 双路径分发：矩阵类 vs 顶层类

三个公开操作均以 `PanelKind` 做顶部分发：

```
if panelKind == Namespace → OnNamespaceXxx / BroadcastNamespace
if panelKind == HybridCLR (#if UNITY_EDITOR) → OnHybridCLRXxx / BroadcastHybridCLR
if panelKind == YooAsset  (#if UNITY_EDITOR) → OnYooAssetXxx / BroadcastYooAsset
else (Common / SDK / Kit) → 矩阵类路径（m_Entries 遍历）
```

**矩阵类路径**（Common / SDK / Kit）：
- Common 使用 C# 层逐字段深拷贝（`DeepCloneCommon`，对齐 `Exporter.CloneCommon`）
- SDK / Kit 使用 `DeepCloneManagedRef`（`JsonUtility` round-trip）产生内存态独立深拷贝，保留 `[SerializeReference]` 多态类型

**顶层类路径**（Namespace / HybridCLR / YooAsset）：
- 底座为 `ConfigMasterSO.XxxOverrides` 列表 + 顶层默认字段
- 加维分裂：`UpsertXxxOverride`（找同组首条更新 / 无则追加）
- 减维合并：`RemoveAll(同组条目)` + 有勾轴时保留代表格 / IsGlobal 时回写顶层字段
- 广播：遍历 `XxxOverrides` 覆盖同组条目值

### GroupMembers — 同组格枚举

遍历 `m_Entries`（Platform×Channel）× `{Debug, Publish}` 全量后按掩码过滤：
- 掩码勾选的轴：只允许与 `coord` 同值的格
- 掩码未勾选的轴：允许所有取值（等价于该轴维度无区别）

跳过 `Platform == None` 或 `Channel == None` 的占位行。

### DeepCloneManagedRef — SerializeReference 跨格深拷贝（内存态独立）

`FillGroupSerializedRef` 调用此 helper 将源格的 `[SerializeReference]` 多态对象写入目标格，保证内存态实例独立：

```
srcElemProp.boxedValue
  → DeepCloneManagedRef(src)
      ├─ Activator.CreateInstance(src.GetType())   ← 产生同类型新实例
      ├─ JsonUtility.ToJson(src)                   ← 序列化为 JSON
      └─ JsonUtility.FromJsonOverwrite(json, copy) ← 字段值写入新实例
  → dstElemProp.boxedValue = copy                  ← 写回目标格（实例独立）
```

**为何弃用 `boxedValue` 直接赋值：**
`boxedValue` getter/setter 对 `SerializeReference` 在**编辑期内存态**操作的是同一对象引用，而非深拷贝。实测 `ReferenceEquals(Debug 格 LoginKitConfig, Publish 格 LoginKitConfig) = True`；修改 Publish 字段会同步污染 Debug，导致跨格编辑互相干扰。`CopySerialized` round-trip（存盘后重序列化）确实可产生独立实例，但用户编辑操作发生在内存态 working 上，存盘路径不覆盖此场景。

**约束：** SDK/Kit 配置类必须是 `JsonUtility` 可序列化的叶子数据（`[Serializable]` 简单值字段），禁止内嵌 `[SerializeReference]` 多态字段——`JsonUtility` 不保留嵌套多态，届时子引用类型会丢失。目标格元素不存在时先调 `EnsureConfigInstance`（反射创建无参实例追加到列表，`masterSO.Update()` 刷新）再写入。

### ClipCoordToMask — 坐标裁剪

按掩码只保留勾选轴的分量，未勾选轴填 `None` 哨兵（`DevelopMode` 无 `None`，始终保留原值）。用于 Override 条目的存储坐标，与 `DimensionalResolver.MatchesMask` 的匹配算法对称。

---

## §10 常见误区

| 误区 | 正确做法 |
|------|---------|
| 直接修改 `m_Entries` 某格数据并期望其他同组格同步 | 修改后调用 `BroadcastWithinGroup`，由 ConfigWindow 负责触发 |
| 在 `PanelKind.Common` 情况下传非 null `masterSO` 并依赖其生效 | Common 路径走 C# 层拷贝，不经 `SerializedObject`；`masterSO` 参数在 Common 分支中实际未使用 |
| 切换维度后跳过 `ApplyModifiedPropertiesWithoutUndo` 直接保存 | SDK/Kit 路径的 `FillGroupSerializedRef` 通过 `SerializedProperty` 写入，必须经 `ApplyModifiedPropertiesWithoutUndo` 才能同步回 C# 层 |
| 顶层类维度切换后不刷 `YooAssetInjector.Inject` | YooAsset mask 变更后路径已更新但注入还是旧值；`ConfigWindow.RightPanel.YooAsset.cs` 的 `ReInjectYooAsset` 需在 toggle 回调中调用 |
| SDK/Kit 分支先 `masterSO.Update()` 再 `SetAxis(mask, axis, value)`（旧顺序） | `mask` 是绕过 `SerializedProperty` 直改的 C# 字段；先 Update 则 SO 缓存中 mask 仍为旧值（stale）；后续 `ApplyModifiedPropertiesWithoutUndo` 把整棵 SO 缓存回写 native 时会用 stale 旧值覆盖（clobber）新 mask。**正确顺序：`SetAxis` → `masterSO.Update()` → `FindSerializedRefProp` → Apply**，确保 Update 把含新 mask 值的 working 读入 SO 缓存，Apply 时不再 clobber。此问题在 ConfigWindow 开启 `editingTextField` skip-Update 优化后尤为明显（编辑期 SO 缓存持续 stale，编辑提交 Apply 时必然 clobber）。 |
| `dstElemProp.boxedValue = srcElemProp.boxedValue` 用于 SerializeReference 跨格拷贝 | `boxedValue` 对 `[SerializeReference]` 字段在**编辑期内存态**返回/写入的是同一对象引用，`ReferenceEquals=True`；跨格编辑会互相污染。**正确做法：通过 `DeepCloneManagedRef` 经 `JsonUtility` round-trip 产生内存态独立新实例后再赋值。** 约束：SDK/Kit 配置类不得内嵌 `[SerializeReference]` 多态字段，`JsonUtility` 不保留嵌套多态。 |

---

## §11 使用示例

```csharp
// ConfigWindow.RightPanel.cs 中勾选 ByPlatform 时触发
var curCoord = new DimensionProjector.Coord(
    m_Master.CurrentPlatform,
    m_Master.CurrentChannel,
    m_Master.CurrentDevelopMode);

// 加维分裂（Common 面板勾选 Platform 轴）
DimensionProjector.OnDimensionEnabled(
    workingCopy,          // m_WorkingCopy（ConfigMasterSO，内存暂存）
    workingCopySO,        // SerializedObject(workingCopy)
    DimensionProjector.PanelKind.Common,
    null,
    curCoord,
    DimensionProjector.DimensionAxis.Platform);

// 编辑后广播（SDK 面板某字段变更）
DimensionProjector.BroadcastWithinGroup(
    workingCopy,
    workingCopySO,
    DimensionProjector.PanelKind.SDK,
    "NovaFramework.WeChat.WeChatSDKPluginConfig",
    curCoord);
```

---

## §13 关联文档

- [PanelDimensionMask.md](../../../Runtime/Modules/Config/Definitions/PanelDimensionMask.md)（掩码类型）
- [EditorUtil.Config.DimensionalResolver.md](EditorUtil.Config.DimensionalResolver.md)（只读取数对称类）
- [ConfigMasterSO.md](../../../Runtime/Modules/Config/ConfigMasterSO.md)（`CommonMask` / `SDKMasks` / `KitMasks` / `NamespaceMask` / `HybridCLRMask` / `YooAssetMask` + `XxxOverrides` 字段）
- [ConfigWindow.md](../../Windows/ConfigWindow.md)（调用方，在 `DrawDimensionMaskRow` / `DrawYooAssetTitleWithMask` 中触发三操作）
