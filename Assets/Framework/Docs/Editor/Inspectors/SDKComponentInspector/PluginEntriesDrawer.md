# PluginEntriesDrawer

**类签名**：`internal sealed class PluginEntriesDrawer : IDisposable`
**命名空间**：`NovaFramework.Editor`
**所属文件**：`SDKComponentInspector.PluginEntriesDrawer.cs`
**宿主类**：`SDKComponentInspector`（嵌套类）

Plugin 条目列表绘制器，封装反射扫描、分组渲染与 Missing 清理逻辑。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|-----|------|
| `SDKComponentInspector.PluginEntriesDrawer.cs` | `SDKComponentInspector.PluginEntriesDrawer` | 本类全部实现（嵌套于 `SDKComponentInspector`） |

---

## §3 继承关系

```
System.IDisposable
  └── PluginEntriesDrawer (internal sealed，嵌套于 SDKComponentInspector)
```

---

## §4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `c_DefaultPriority` | `const int` | `100` | 新 append 条目的初始 Priority |
| `m_ScannedPluginTypes` | `List<Type>` | `new List<Type>()` | 每次 `SyncEntries` 刷新的非抽象 `ISDKPlugin` 实现类型列表 |
| `m_RedLabelStyle` | `GUIStyle` | `null` | 红色标签样式（延迟初始化）；`Dispose` 时置 `null` |

---

## §5 完整公开 API

### IDisposable

```csharp
// 清理所有已追踪 Foldout 的全局缓存；将 m_RedLabelStyle 置 null
public void Dispose()
```

### 同步

```csharp
// 增量同步：反射扫描 ISDKPlugin 实现类 → append 新 Entry → 标记 IsMissing
// 若有写入则调用 so.ApplyModifiedProperties()
public void SyncEntries(SerializedProperty entriesProp, SerializedObject so)
```

### 绘制

```csharp
// 绘制整个 Plugin 列表区域：分组折叠展示 + Missing 区域 + 分隔线
public void Draw(SerializedProperty entriesProp, SerializedObject so)
```

---

## §9 关键算法

### SyncEntries 三阶段流程

```
SyncEntries(entriesProp, so)
  ├─ RefreshScannedTypes()
  │    └─ TypeCache.GetTypesDerivedFrom<ISDKPlugin>() 过滤 IsAbstract / IsInterface
  ├─ AppendMissingTypes(entriesProp) → dirty
  │    ├─ 先扫现有 entriesProp 统计各族是否已有条目（6 个 bool：hasExisting*）
  │    └─ 遍历 m_ScannedPluginTypes；按 AssemblyQualifiedName 查 EntryIndexOf；
  │       不存在 → arraySize+1，写 TypeName / Priority=100
  │       Enabled 初值规则：
  │         - 若该族 append 前无任何条目（hasExisting*=false）且为本轮首个 append（firstAppended*=false）
  │           → Enabled=true（面板默认显示并选中第一个，数据与显示一致）
  │         - 其余情况 → Enabled=false（不覆盖用户既有选择）
  │       族归属判定顺序（与 DrawGroupedEntries 调用顺序一致）：
  │         Normal > Monetize > Ad > Attribution > Account > Cloud
  └─ MarkMissingEntries(entriesProp)
       └─ 通过 targetObject 直接访问 component.PluginEntries（[NonSerialized] IsMissing 不能走 SerializedProperty）
          entry.IsMissing = !string.IsNullOrEmpty(entry.TypeName) && Type.GetType(entry.TypeName) == null
```

### 接口族分组规则

| 分组名 | 判定接口 | 备注 |
|--------|---------|------|
| 普通埋点 | `ITrackPlugin`（排除 `IMonetizeTrackPlugin` 和 `IAttributionPlugin`） | IsNormalTrackPlugin |
| 变现埋点 | `IMonetizeTrackPlugin` | IsMonetizeTrackPlugin |
| 广告 | `IAdPlugin` | IsAdPlugin |
| 归因埋点 | `IAttributionPlugin` | IsAttributionPlugin |
| 账号登录 | `IAuthPlugin` | IsAccountPlugin |
| 云服务 | `IPushPlugin` 或 `IRemoteConfigPlugin` | IsCloudPlugin |

族归属取第一个匹配谓词（按上表从上到下），与面板 `DrawGroupedEntries` 调用顺序一致。

### DrawGroupSelector Popup 语义

- `options[0] = "无"`，`options[i+1] = groupTypes[i].FullName`（i 从 0 起）
- `curPopupIndex = 0` 表示该族全 false（无选中）；`curPopupIndex = i+1` 表示 `groupIndices[i]` 对应条目 Enabled=true
- 写回：`Enabled = (newPopupIndex > 0 && i == newPopupIndex - 1)`
- 切到 0（"无"）→ 所有同族条目 Enabled=false（用户主动选"无"，后续帧不再自动恢复）

### Missing 判定逻辑

```
条目 Missing 条件（满足任一）：
  1. TypeName 为 null 或空字符串（string.IsNullOrEmpty）
  2. Type.GetType(TypeName) 返回 null（程序集已卸载或类名变更）
```

### RemoveAllMissingEntries

```
1. 遍历 entriesProp.arraySize，收集 Missing 索引 → toRemove 列表
2. 逆序删除（防止前删后移导致索引偏移）
3. 立即 entriesProp.serializedObject.ApplyModifiedProperties()
   ↑ 必须内部 Apply：DangerButton 触发 ExitGUI 会中断后续执行，
     若不在此处提交，变更将丢失
```

---

## §10 常见误区

**误区 1：认为 Missing 标记会被序列化**
`SDKPluginEntry.IsMissing` 标注了 `[NonSerialized]`，`MarkMissingEntries` 必须通过 `targetObject` 直接写入运行时实例，不能用 `FindPropertyRelative("IsMissing")`。

**误区 2：直接 Apply 后再删除**
`RemoveAllMissingEntries` 内部已调用 `ApplyModifiedProperties`，外层调用者（`DrawMissingSection`）不应再 Apply，否则产生重复提交。

**误区 3：认为分组是互斥的**
分组基于接口判断，实现多个子接口的 Plugin 会同时出现在多个分组，这是预期行为。

**误区 4：用"当前族全 false 就自动启用第一个"修复默认选中问题**
此做法会在用户主动将某族切为"无"后，下一帧 `DrawGroupSelector` 重新检测到全 false 并再次启用第一个，导致用户无法持久选"无"。正确做法是仅在 `AppendMissingTypes`（首次 append 那一刻）对新族首条目置 `Enabled=true`，后续绘制帧不干预数据。

**误区 5：Popup 无"无"首项时，单插件族用户永远无法触发 EndChangeCheck 写回**
若 `options` 直接从 `groupTypes[0]` 开始，`curPopupIndex` 默认 0，用户看到的是"第 0 个插件被选中"但 `Enabled` 并没有被写入。切不到其他项时 `EndChangeCheck` 永远为 false，`Enabled` 停在 `AppendMissingTypes` 写入的初值。正确做法是 `options[0]="无"`，`options[i+1]=FullName`，并在 `AppendMissingTypes` 将首条目 `Enabled=true`，面板与数据天然一致。

---

## §11 使用示例

```csharp
// PluginEntriesDrawer 由 SDKComponentInspector 持有，典型用法：

// OnEnable 中构造并初始同步
m_Drawer = new PluginEntriesDrawer();
m_Drawer.SyncEntries(m_PluginEntries, serializedObject);

// OnInspectorGUI 中每帧调用
m_Drawer.SyncEntries(m_PluginEntries, serializedObject);   // 增量同步
m_Drawer.Draw(m_PluginEntries, serializedObject);          // 绘制列表

// OnDisable 中释放
m_Drawer?.Dispose();
m_Drawer = null;
```

---

## §12 注意事项

- `GUIStyle m_RedLabelStyle` 必须延迟初始化（`GetRedLabelStyle` 方法），静态构造期 `EditorStyles` 尚未就绪。
- `DrawGroupSelector` 对同族所有条目写回时，在 `EndChangeCheck` 回调内统一调用一次 `so.ApplyModifiedProperties()`，避免多次 Apply 产生快照冲突。
- `AppendMissingTypes` 中的"首 append 自动启用"逻辑依赖"append 前扫描已有条目"的快照。若同一帧内多次调用 `AppendMissingTypes`（正常不会），后一次调用的快照包含了前一次 append 的结果，行为仍然正确（前一次已将 `hasExisting*` 置 true）。
- `DrawGroupSelector` 是 `private static`，不持有任何状态，可安全在多族中复用。

---

## §13 关联文档

- [SDKComponentInspector.md](./SDKComponentInspector.md) — 宿主 Inspector
- [Definitions/SDKPluginEntry.md](../../../Runtime/Modules/SDK/Definitions/SDKPluginEntry.md) — Plugin 条目序列化结构（含 IsMissing 字段说明）
- [Definitions/ISDKPlugin.md](../../../Runtime/Modules/SDK/Definitions/ISDKPlugin.md) — Plugin 基接口
- [EditorUtil.Draw.md](../../EditorUtil/EditorUtil.Draw/EditorUtil.Draw.md) — Foldout / DangerButton / CleanFoldout 等 GUI 工具
