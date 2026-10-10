# EditorUtil.Config.DimensionProjector

**类签名**：`public static class DimensionProjector`（嵌套于 `EditorUtil.Config`）
**命名空间**：`NovaFramework.Editor`

Config 面板维度投影器；按 `PanelDimensionMask` 在 `m_WorkingCopy` 上执行普通编辑广播、加维分裂和减维合并，覆盖矩阵类 App / Privacy / SDK / Kit 与顶层 Namespace / HybridCLR / YooAsset / CDN 八类面板。所有操作只修改内存副本，不直接落盘；ConfigWindow 负责标记脏状态并在用户点击保存后整体写回。`Coord` 是显式三维值类型，Platform 来自 ConfigWindow 的编辑平台。

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
              ├── PanelKind (public enum)          — 八种面板
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
// 面板种类：区分四种矩阵面板和四种顶层 Override 面板
public enum PanelKind
{
    AppConfigs,
    PrivacyConfigs,
    SDK,
    Kit,
    Namespace,
    HybridEditorConfigs,
    YooAssetEditorConfigs,
    CDNEditorConfigs,
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
// 加维分裂：冻结旧掩码下的全部逻辑组，再为新轴的每个取值建立独立副本
public static void OnDimensionEnabled(
    ConfigMasterSO master,
    SerializedObject masterSO,   // 绑定 WorkingCopy，用于先提交字段缓冲并在投影后刷新
    PanelKind panelKind,
    string typeName,             // SDK 或 Kit 类型全名；其它面板忽略
    Coord curCoord,
    DimensionAxis axis);

// 减维合并：对新掩码下的每个剩余逻辑组，分别保留顶部当前轴取值对应的旧分支
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

ConfigWindow 保存前调用 internal `NormalizeForSave(master, masterSO, curCoord)`，在 WorkingCopy 上自动按当前掩码归一，无需整理确认。`curCoord.Platform` 必须使用窗口编辑平台。归一完成且校验通过后才 `CopySerialized` 落盘；Exporter 保持只读门禁，不隐式归一或写 Master。

资源加解密配置通过 internal `OnDimensionEnabled(ResourceKeyStoreData, Coord, DimensionAxis)` / `OnDimensionDisabled(...)` 和 `SetResourceActiveKeyId(...)` 接入 ConfigMaster 工作副本中的资源密钥配置。它只投影当前加密编号选择，全部解密密钥保留；通过顶部全局保存写入 ConfigMaster，但不参与 ConfigRuntime 导出。

---

## §9 关键算法

### 全矩阵原子投影

维度切换先冻结旧掩码，再枚举新掩码下的全部逻辑组。旧掩码已勾选的轴沿用各组自己的值，旧掩码未勾选的轴使用顶部当前值确定来源。所有来源都成功拍快照后才修改掩码和数据；中途失败会用完整备份恢复 WorkingCopy。

- 勾选轴：每个旧逻辑组分别拆成新轴的多份，不能只复制顶部当前组合。
- 取消轴：每个剩余逻辑组分别保留顶部当前轴值对应的分支，不能拿一个完整坐标覆盖全矩阵。
- 矩阵类写回每个物理格；SDK / Kit 每格使用独立 `SerializeReference` 深拷贝。
- 顶层类清理旧 Override 后，按新掩码每个逻辑键重建一条规范 Override；全局模式回写顶层字段。

### 保存自动归一

`NormalizeForSave` 不改变维度开关，原子整理全部配置：

- 无效平台、渠道、模式及空槽清理，缺失物理矩阵按显式编辑平台补齐；真实重复有效坐标在投影前拒绝。
- App / Privacy 和所有实际存储的 SDK / Kit 按当前掩码逐逻辑组取值，同步未选轴，保留已选轴。SDK / Kit 稀疏组在当前来源缺失时只继承同组唯一值；已符合掩码的实例保持原引用，避免重复保存产生引用标识噪声。
- Namespace / HybridCLR / YooAsset / CDN 全局时保留顶层当前值并清空 Overrides。部分维度优先规范裁剪键，再匹配当前未选轴的旧具体坐标，清除非当前分支并重建规范逻辑组；同优先级不同内容不得按列表顺序猜测。
- ResourceEncryption 只整理 `ActiveKeyOverrides`，保留全部 `Keys` 和独立源码导出目录。
- 完成维度及密钥校验后才返回；异常恢复完整 WorkingCopy，资产不被覆盖。

### GroupMembers — 同组格枚举

遍历 `m_Entries`（Platform×Channel）× `DevelopMode` 枚举全集后按掩码过滤：
- 掩码勾选的轴：只允许与 `coord` 同值的格
- 掩码未勾选的轴：允许所有取值（等价于该轴维度无区别）

物理矩阵只处理 Platform 与 Channel 均非 `None` 的有效坐标；`None` 仅允许出现在 Override 的未参与轴哨兵中，不作为实际矩阵行参与分组与广播。

### DeepCloneManagedRef — SerializeReference 跨格深拷贝（内存态独立）

SDK / Kit 广播与投影调用此 helper 将源格的 `[SerializeReference]` 多态对象写入目标格，保证内存态实例独立：

```
源配置对象
  → DeepCloneManagedRef(src)
      ├─ Activator.CreateInstance(src.GetType())   ← 产生同类型新实例
      ├─ JsonUtility.ToJson(src)                   ← 序列化为 JSON
      └─ JsonUtility.FromJsonOverwrite(json, copy) ← 字段值写入新实例
  → 替换目标格同类型配置                           ← 写回目标格（实例独立）
```

**为何弃用 `boxedValue` 直接赋值：**
直接赋值 `SerializeReference` 对象会让多个格共享同一引用；修改其中一格会同步污染其它格。投影器改为先创建同类型实例，再通过 `JsonUtility` 复制字段。

**约束：** SDK/Kit 配置类必须是 `JsonUtility` 可序列化的叶子数据（`[Serializable]` 简单值字段），禁止内嵌 `[SerializeReference]` 多态字段——`JsonUtility` 不保留嵌套多态，届时子引用类型会丢失。目标格没有同类型实例时，投影器会把深拷贝实例加入对应配置列表。

### ClipCoordToMask — 坐标裁剪

按掩码只保留勾选轴的分量，未勾选的 Platform / Channel 填 `None`，未勾选的 DevelopMode 填默认值 `Debug`。用于 Override 条目的存储坐标，与 `DimensionalResolver.MatchesMask` 的匹配算法对称。

---

## §10 常见误区

| 误区 | 正确做法 |
|------|---------|
| 直接修改 `m_Entries` 某格数据并期望其他同组格同步 | 修改后调用 `BroadcastWithinGroup`，由 ConfigWindow 负责触发 |
| 保存时用一个完整坐标覆盖全部组 | 未选轴取当前值，已选轴逐组保留；自动清理旧分支并重新校验 |
| 开启维度时只复制当前切片 | 先枚举旧掩码的全部逻辑组，再对新轴完整拆分 |
| 取消维度时拿当前完整坐标覆盖全矩阵 | 对每个剩余逻辑组分别保留顶部当前轴值对应的旧分支 |
| 顶层类维度切换后不刷 `YooAssetInjector.Inject` | YooAsset mask 变更后路径已更新但注入还是旧值；`ConfigWindow.RightPanel.YooAsset.cs` 的 `ReInjectYooAsset` 需在 toggle 回调中调用 |
| 直接复用 SDK/Kit 的配置对象引用 | 通过 `DeepCloneManagedRef` 为每个物理格生成独立实例；SDK/Kit 配置类不得内嵌 `JsonUtility` 无法保留的多态子引用 |

---

## §11 使用示例

```csharp
// ConfigWindow.RightPanel.cs 中勾选 ByPlatform 时触发；平台来自窗口本地编辑选择
var curCoord = new DimensionProjector.Coord(
    editingPlatform,
    m_Master.CurrentChannel,
    m_Master.CurrentDevelopMode);

// 加维分裂（AppConfigs 面板勾选 Platform 轴）
DimensionProjector.OnDimensionEnabled(
    workingCopy,          // m_WorkingCopy（ConfigMasterSO，内存暂存）
    workingCopySO,        // SerializedObject(workingCopy)
    DimensionProjector.PanelKind.AppConfigs,
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

- [PanelDimensionMask.md](../../../Editor/Config/Definitions/PanelDimensionMask.md)（掩码类型）
- [EditorUtil.Config.DimensionalResolver.md](EditorUtil.Config.DimensionalResolver.md)（只读取数对称类）
- [ConfigMasterSO.md](../../../Editor/Config/ConfigMasterSO.md)（`AppConfigsMask` / `SDKMasks` / `KitMasks` / `NamespaceMask` / `HybridEditorConfigsMask` / `YooAssetEditorConfigsMask` + `XxxOverrides` 字段）
- [ConfigWindow.md](../../Windows/ConfigWindow.md)（调用方，在 `DrawDimensionMaskRow` / `DrawYooAssetTitleWithMask` 中触发三操作）
