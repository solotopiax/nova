# SDKComponentInspector

**类签名**：`[CustomEditor(typeof(SDKComponent))] internal sealed partial class SDKComponentInspector : BaseComponentInspector`
**命名空间**：`NovaFramework.Editor`
**目标组件**：`NovaFramework.Runtime.SDKComponent`

SDK 模块 Inspector，承担 Manager 选择器与 Plugin 条目列表（分组展示 + Missing 清理）的 Inspector 绘制。Plugin 条目可见性由当前 active `ConfigMaster.EnabledSDKs` 决定。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|-----|------|
| `SDKComponentInspector.cs` | `SDKComponentInspector` | 主体：OnEnable 绑定属性、初始化 Drawer、订阅 Config 保存事件；OnInspectorGUI 调度绘制 |
| `SDKComponentInspector.Visitors.cs` | `SDKComponentInspector` | SerializedProperty 字段与 Drawer 引用声明 |
| `SDKComponentInspector.Methods.cs` | `SDKComponentInspector` | 私有绘制方法：`DrawConfigs` / `DrawPluginEntries` |
| `SDKComponentInspector.PluginEntriesDrawer.cs` | `SDKComponentInspector.PluginEntriesDrawer` | 嵌套类：active ConfigMaster 过滤 + 分组渲染 + Missing 检测与清理 |

---

## §3 继承关系

```
Editor.Editor
  └── UnityEditor.Editor
        └── BaseComponentInspector
              └── SDKComponentInspector (internal sealed partial)
                    └── SDKComponentInspector.PluginEntriesDrawer (内嵌 sealed class)
```

---

## §4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `m_CurManagerTypeName` | `SerializedProperty` | — | 绑定 `SDKComponent.m_CurManagerTypeName` |
| `m_PluginEntries` | `SerializedProperty` | — | 绑定 `SDKComponent.m_PluginEntries` |
| `m_ManagerTypeNames` | `List<string>` | — | `ISDKManager` 所有实现类型名称，供下拉选择 |
| `m_Drawer` | `PluginEntriesDrawer` | — | Plugin 条目列表绘制器，`OnEnable` 构造，`OnDisable` Dispose |

---

## §5 完整公开 API

### Editor 生命周期

```csharp
// 绑定 SerializedProperty，收集 ISDKManager 类型名，构造并初始同步 PluginEntriesDrawer，订阅 Config 保存事件
protected override void OnEnable()

// 取消订阅 Config 保存事件，释放 PluginEntriesDrawer
private void OnDisable()

// ConfigMaster 保存后强制刷新 Drawer 可见 Plugin 缓存并重绘 Inspector
private void OnActiveConfigMasterSaved(ConfigMasterSO master)

// 绘制 Inspector：base.OnInspectorGUI → DrawConfigs → DrawPluginEntries → FinalRefreshInspectorGUI
public override void OnInspectorGUI()
```

### 私有绘制方法

```csharp
// 绘制 SDK 管理器类型选择器（TypesSelector）+ 分隔线
private void DrawConfigs()

// 每帧增量同步 Plugin 条目，再委托 Drawer 绘制分组列表
private void DrawPluginEntries()
```

---

## §6 生命周期

```
OnEnable
  ├─ base.OnEnable()
  ├─ 绑定 m_CurManagerTypeName / m_PluginEntries
  ├─ 收集 m_ManagerTypeNames（ISDKManager 实现名）
  ├─ new PluginEntriesDrawer() → SyncEntries（按 active ConfigMaster 初始同步）
  └─ 订阅 EditorUtil.Config.Events.ActiveConfigMasterSaved

OnInspectorGUI（每帧）
  ├─ base.OnInspectorGUI()
  ├─ DrawConfigs()
  ├─ DrawPluginEntries()        ← SyncEntries + Drawer.Draw
  └─ FinalRefreshInspectorGUI()

OnActiveConfigMasterSaved
  ├─ serializedObject.Update()
  ├─ m_Drawer.ForceRefresh()
  ├─ m_Drawer.SyncEntries(...)
  └─ Repaint()

OnDisable
  ├─ 取消订阅 EditorUtil.Config.Events.ActiveConfigMasterSaved
  └─ m_Drawer?.Dispose()，置 null
```

> Play 模式下，`disableOnPlaying=true`（继承自 `BaseComponentInspector`）自动禁用所有控件，不展示运行时数据面板。

---

## §8 初始化与刷新时序

`OnEnable` 阶段 `PluginEntriesDrawer.SyncEntries` 会按当前 active `ConfigMaster.EnabledSDKs` 扫描可见 Plugin 并同步 Entry。后续每帧 `DrawPluginEntries` 仍调用 `SyncEntries`，用于响应编译刷新、active ConfigMaster 变化和 EnabledSDKs 签名变化。

ConfigWindow 点击保存后会提交 WorkingCopy 到真实 `ConfigMasterSO`，随后广播 `EditorUtil.Config.Events.ActiveConfigMasterSaved`。SDKComponent Inspector 收到事件后强制刷新 Drawer 缓存，因此用户在 ConfigWindow 里取消某个 SDK 并保存后，SDKComponent Inspector 会立刻把对应分组更新为 `无`。

---

## §9 显示语义

- Manager 下拉仍扫描所有 `ISDKManager` 实现。
- Plugin 分组只显示当前 active `ConfigMaster.EnabledSDKs` 映射出的 Plugin。
- `EnabledSDKs` 存的是 `ISDKPluginConfig` 类型 FullName，Drawer 通过 `SDKPluginBase.RequiredConfigType` 映射到 Plugin 类型。
- ConfigMaster 中未启用的旧 `m_PluginEntries` 条目会保留，但不参与显示、默认启用判断或单选互斥写回。
- Missing 区域只显示类型已经无法解析的 Entry，不显示 inactive Entry。

---

## §11 使用示例

```csharp
// Inspector 无需手动使用，Unity 编辑器自动调用。

// 展示结构示意：
// [SDK 管理器]  ▼ NovaFramework.Runtime.SDKManager
// ─────────────────────────────
// 普通埋点      ▼ 无
// 变现埋点      ▼ NovaFramework.SDK.TGA.TGAMonetizeTrackPlugin
// 广告          ▼ NovaFramework.SDK.Max.MaxAdPlugin
// 归因埋点      ▼ 无
// 账号登录      ▼ Multiple Selected
// 云服务        ▼ 无
// 支付          ▼ NovaFramework.SDK.IAP.GooglePlayIAPPlugin
// ─────────────────────────────
// [Missing] OldPlugin
// [ 清理所有 Missing ]
```

---

## §13 关联文档

- [PluginEntriesDrawer.md](./PluginEntriesDrawer.md) — 嵌套绘制器核心逻辑
- [BaseComponentInspector.md](../BaseComponentInspector.md) — Inspector 基类
- [SDKComponent.md](../../../Runtime/Modules/SDK/SDKComponent.md) — 目标 Component
- [Definitions/SDKPluginEntry.md](../../../Runtime/Modules/SDK/Definitions/SDKPluginEntry.md) — Plugin 条目序列化结构
- [Managers/Interfaces/ISDKManager.md](../../../Runtime/Modules/SDK/Managers/Interfaces/ISDKManager.md) — Manager 契约
- [EditorUtil.Config.WorkspaceActive.md](../../EditorUtil/EditorUtil.Config/EditorUtil.Config.WorkspaceActive.md) — active ConfigMaster 与保存事件
