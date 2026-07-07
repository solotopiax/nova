# PluginEntriesDrawer

**类签名**：`internal sealed class PluginEntriesDrawer : IDisposable`
**命名空间**：`NovaFramework.Editor`
**所属文件**：`SDKComponentInspector.PluginEntriesDrawer.cs`
**宿主类**：`SDKComponentInspector`（嵌套类）

Plugin 条目列表绘制器，封装 active ConfigMaster 驱动的 Plugin 可见列表同步、分组渲染与 Missing 清理逻辑。

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
| `m_ScannedPluginTypes` | `List<Type>` | `new List<Type>()` | 当前 active `ConfigMaster.EnabledSDKs` 映射出的可见 Plugin 类型列表 |
| `m_VisiblePluginTypeNames` | `HashSet<string>` | `new HashSet<string>()` | 当前可见 Plugin 的 `AssemblyQualifiedName` 集合，用于过滤旧 Entry |
| `m_LastActiveMasterAssetPath` | `string` | `null` | 上次刷新时的 active ConfigMaster 资产路径 |
| `m_LastEnabledSDKSignature` | `string` | `null` | 上次刷新时 `EnabledSDKs` 的排序签名，避免每帧重复扫描 |
| `m_RedLabelStyle` | `GUIStyle` | `null` | 红色标签样式（延迟初始化）；`Dispose` 时置 `null` |

---

## §5 完整公开 API

```csharp
// 清理 m_RedLabelStyle
public void Dispose()

// 增量同步：按 active ConfigMaster.EnabledSDKs 刷新可见 Plugin → append 新 Entry → 标记 IsMissing
// 若有写入则调用 so.ApplyModifiedProperties()
public void SyncEntries(SerializedProperty entriesProp, SerializedObject so)

// 绘制整个 Plugin 列表区域：分组下拉 + Missing 区域 + 分隔线
public void Draw(SerializedProperty entriesProp, SerializedObject so)

// 强制下次 SyncEntries 重建 active ConfigMaster 可见 Plugin 缓存
public void ForceRefresh()
```

---

## §9 关键算法

### SyncEntries 三阶段流程

```
SyncEntries(entriesProp, so)
  ├─ RefreshScannedTypes()
  │    ├─ EditorUtil.Config.WorkspaceActive.Get() 获取 active ConfigMaster
  │    ├─ 读取 master.EnabledSDKs（存的是 ISDKPluginConfig 类型 FullName）
  │    ├─ TypeCache.GetTypesDerivedFrom<ISDKPlugin>() 扫描候选 Plugin
  │    └─ 仅保留继承 SDKPluginBase 且 RequiredConfigType.FullName 命中 EnabledSDKs 的 Plugin
  ├─ AppendMissingTypes(entriesProp) → dirty
  │    ├─ 只统计当前可见 Entry 是否已有对应族条目
  │    └─ 只为当前可见 Plugin append 新 Entry；旧的 inactive Entry 保留但不参与显示和默认启用判断
  └─ MarkMissingEntries(entriesProp)
       └─ TypeName 存在但 Type.GetType(TypeName) 失败时标记 IsMissing
```

`ConfigMaster.EnabledSDKs` 的条目是 SDK Plugin Config 类型全名，不是 Plugin 类型名。Inspector 通过实例化 `SDKPluginBase` 读取 `RequiredConfigType` 完成 Config → Plugin 映射。

### active ConfigMaster 过滤语义

- 无 active ConfigMaster，或 active ConfigMaster 的 `EnabledSDKs` 为空时，不显示任何可启用 Plugin，各分组显示 `无`。
- 某个 SDK Config 从 ConfigWindow 中取消勾选并保存后，对应 Plugin 会从 SDKComponent Inspector 分组中隐藏。
- 已存在于 `m_PluginEntries` 的旧 Entry 不会因为 ConfigMaster 取消勾选而删除，也不会被标记为 Missing；它只是 inactive，不参与当前面板显示。
- ConfigWindow 保存成功后会广播 `EditorUtil.Config.Events.ActiveConfigMasterSaved`，`SDKComponentInspector` 收到后调用 `ForceRefresh()` 并重新 `SyncEntries()`。

### 接口族分组规则

| 分组名 | 判定接口 | 备注 |
|--------|---------|------|
| 普通埋点 | `ITrackPlugin`（排除 `IMonetizeTrackPlugin` 和 `IAttributionPlugin`） | IsNormalTrackPlugin |
| 变现埋点 | `IMonetizeTrackPlugin` | IsMonetizeTrackPlugin |
| 广告 | `IAdPlugin` | IsAdPlugin |
| 归因埋点 | `IAttributionPlugin` | IsAttributionPlugin |
| 账号登录 | `IAuthPlugin` | IsAccountPlugin，允许多选 |
| 云服务 | `IPushPlugin` 或 `IRemoteConfigPlugin` | IsCloudPlugin |
| 支付 | `IIAPPlugin` | IsIAPPlugin |

族归属取第一个匹配谓词，与面板 `DrawGroupedEntries` 调用顺序一致。

### DrawGroupSelector Popup 语义

- `options[0] = "无"`，`options[i+1] = groupTypes[i].FullName`。
- `curPopupIndex = 0` 表示该族全 false；`curPopupIndex = i+1` 表示 `groupIndices[i]` 对应条目 `Enabled=true`。
- 切到 0（`无`）时，当前可见同族条目全部写为 `Enabled=false`。
- 该族没有可见 Plugin 时，显示禁用的 `无` 占位行。

### DrawAccountMultiSelect 显示语义

- 账号登录族允许多选，写回仍逐条切换 `Enabled`。
- 下拉菜单项显示完整命名空间类型名。
- 按钮摘要：未选显示 `无`；单选显示完整命名空间类型名；多选显示 `Multiple Selected`。
- 没有可见账号 Plugin 时，显示禁用的 `无`。

### Missing 判定逻辑

```
条目 Missing 条件：
  TypeName 非空，且 Type.GetType(TypeName) 返回 null
```

inactive Entry 不属于 Missing。只有插件类型真的无法解析时才进入 Missing 区域。

### RemoveAllMissingEntries

```
1. 遍历 entriesProp.arraySize，收集 TypeName 为空或类型不存在的索引
2. 逆序删除
3. 立即 entriesProp.serializedObject.ApplyModifiedProperties()
```

`DangerButton` 可能触发 `ExitGUI`，因此删除后必须在方法内部提交。

---

## §10 常见误区

**误区 1：认为 EnabledSDKs 存的是 Plugin 类型名**
`EnabledSDKs` 存的是 `ISDKPluginConfig` 类型 FullName。SDKComponent Inspector 必须通过 `SDKPluginBase.RequiredConfigType` 映射到 Plugin 类型。

**误区 2：ConfigMaster 取消勾选后要删除 m_PluginEntries 旧条目**
不删除。取消勾选只影响当前可见列表，旧 Entry 保留，避免用户在不同 ConfigMaster 或不同 SDK 组合之间切换时丢失 Inspector 选型数据。

**误区 3：inactive Entry 等同 Missing**
不是。inactive 表示当前 active ConfigMaster 未启用对应 Config；Missing 表示 `Type.GetType(TypeName)` 已无法解析类型。

**误区 4：认为分组是互斥的**
分组基于接口判断，实现多个子接口的 Plugin 会按调用顺序进入对应分组；账号登录族允许多选，其余族单选。

**误区 5：用“当前族全 false 就自动启用第一个”修复默认选中问题**
此做法会让用户无法持久选择 `无`。正确做法是只在首次 append 可见 Plugin 时，为新族首条目设置默认启用。

---

## §11 使用示例

```csharp
// OnEnable 中构造并初始同步
m_Drawer = new PluginEntriesDrawer();
m_Drawer.SyncEntries(m_PluginEntries, serializedObject);
EditorUtil.Config.Events.ActiveConfigMasterSaved += OnActiveConfigMasterSaved;

// OnInspectorGUI 中每帧调用
m_Drawer.SyncEntries(m_PluginEntries, serializedObject);
m_Drawer.Draw(m_PluginEntries, serializedObject);

// ConfigMaster 保存后强制刷新缓存
m_Drawer.ForceRefresh();
m_Drawer.SyncEntries(m_PluginEntries, serializedObject);

// OnDisable 中释放
EditorUtil.Config.Events.ActiveConfigMasterSaved -= OnActiveConfigMasterSaved;
m_Drawer?.Dispose();
m_Drawer = null;
```

---

## §12 注意事项

- `GUIStyle m_RedLabelStyle` 必须延迟初始化，静态构造期 `EditorStyles` 尚未就绪。
- `DrawGroupSelector` 和 `DrawAccountMultiSelect` 依赖 `m_VisiblePluginTypeNames`，因此是实例方法。
- `AppendMissingTypes` 中的首 append 自动启用逻辑只针对当前可见 Plugin，不覆盖用户已选择 `无` 的状态。
- `RemoveAllMissingEntries` 只清理真正 Missing 的条目，不清理 inactive 旧条目。

---

## §13 关联文档

- [SDKComponentInspector.md](./SDKComponentInspector.md) — 宿主 Inspector
- [Definitions/SDKPluginEntry.md](../../../Runtime/Modules/SDK/Definitions/SDKPluginEntry.md) — Plugin 条目序列化结构（含 IsMissing 字段说明）
- [Definitions/ISDKPlugin.md](../../../Runtime/Modules/SDK/Definitions/ISDKPlugin.md) — Plugin 基接口
- [Definitions/SDKPluginBase.md](../../../Runtime/Modules/SDK/Definitions/SDKPluginBase.md) — `RequiredConfigType` 映射来源
- [EditorUtil.Config.WorkspaceActive.md](../../EditorUtil/EditorUtil.Config/EditorUtil.Config.WorkspaceActive.md) — active ConfigMaster 获取与保存事件
