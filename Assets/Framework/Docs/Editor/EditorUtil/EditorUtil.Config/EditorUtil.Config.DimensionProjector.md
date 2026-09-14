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

保存阶段不调用投影器。ConfigWindow 会先提交 `SerializedProperty` 缓冲并校验维度不变量，再通过 `CopySerialized(m_WorkingCopy, m_Master)` 完整落盘；Exporter 在所有入口执行同一只读门禁。

---

## §9 关键算法

### 全矩阵原子投影

维度切换先冻结旧掩码，再枚举新掩码下的全部逻辑组。旧掩码已勾选的轴沿用各组自己的值，旧掩码未勾选的轴使用顶部当前值确定来源。所有来源都成功拍快照后才修改掩码和数据；中途失败会用完整备份恢复 WorkingCopy。

- 勾选轴：每个旧逻辑组分别拆成新轴的多份，不能只复制顶部当前组合。
- 取消轴：每个剩余逻辑组分别保留顶部当前轴值对应的分支，不能拿一个完整坐标覆盖全矩阵。
- 矩阵类写回每个物理格；SDK / Kit 每格使用独立 `SerializeReference` 深拷贝。
- 顶层类清理旧 Override 后，按新掩码每个逻辑键重建一条规范 Override；全局模式回写顶层字段。

### GroupMembers — 同组格枚举

遍历 `m_Entries`（Platform×Channel）× `DevelopMode` 枚举全集后按掩码过滤：
- 掩码勾选的轴：只允许与 `coord` 同值的格
- 掩码未勾选的轴：允许所有取值（等价于该轴维度无区别）

仅跳过 `Platform == None` 的占位行；`Channel == None` 是合法的无特定运营渠道坐标，会正常参与分组与广播。

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
| 点击保存时再广播当前格 | 保存只校验并完整复制 WorkingCopy；广播必须发生在字段实际变化时，维度全矩阵投影只发生在用户确认开关时 |
| 开启维度时只复制当前切片 | 先枚举旧掩码的全部逻辑组，再对新轴完整拆分 |
| 取消维度时拿当前完整坐标覆盖全矩阵 | 对每个剩余逻辑组分别保留顶部当前轴值对应的旧分支 |
| 顶层类维度切换后不刷 `YooAssetInjector.Inject` | YooAsset mask 变更后路径已更新但注入还是旧值；`ConfigWindow.RightPanel.YooAsset.cs` 的 `ReInjectYooAsset` 需在 toggle 回调中调用 |
| 直接复用 SDK/Kit 的配置对象引用 | 通过 `DeepCloneManagedRef` 为每个物理格生成独立实例；SDK/Kit 配置类不得内嵌 `JsonUtility` 无法保留的多态子引用 |

---

## §11 使用示例

```csharp
// ConfigWindow.RightPanel.cs 中勾选 ByPlatform 时触发；平台来自窗口本地编辑选择
var curCoord = new DimensionProjector.Coord(
    m_Master.CurrentPlatform,
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
