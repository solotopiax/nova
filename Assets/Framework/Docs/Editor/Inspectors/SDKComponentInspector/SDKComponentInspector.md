# SDKComponentInspector

**类签名**：`[CustomEditor(typeof(SDKComponent))] internal sealed partial class SDKComponentInspector : BaseComponentInspector`
**命名空间**：`NovaFramework.Editor`
**目标组件**：`NovaFramework.Runtime.SDKComponent`

SDK 模块 Inspector，承担 Manager 选择器与 Plugin 条目列表（分组展示 + Missing 清理）的 Inspector 绘制。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|-----|------|
| `SDKComponentInspector.cs` | `SDKComponentInspector` | 主体：OnEnable 绑定属性与初始化 Drawer；OnInspectorGUI 调度绘制 |
| `SDKComponentInspector.Visitors.cs` | `SDKComponentInspector` | SerializedProperty 字段与 Drawer 引用声明 |
| `SDKComponentInspector.Methods.cs` | `SDKComponentInspector` | 私有绘制方法：`DrawConfigs` / `DrawPluginEntries` |
| `SDKComponentInspector.PluginEntriesDrawer.cs` | `SDKComponentInspector.PluginEntriesDrawer` | 嵌套类：反射扫描 + 分组渲染 + Missing 检测与清理（见 [PluginEntriesDrawer.md](./PluginEntriesDrawer.md)） |

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
// 绑定 SerializedProperty，收集 ISDKManager 类型名，构造并初始同步 PluginEntriesDrawer
protected override void OnEnable()

// 释放 PluginEntriesDrawer（清理 Foldout 缓存）
private void OnDisable()

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
  └─ new PluginEntriesDrawer() → SyncEntries（初始同步）

OnInspectorGUI（每帧）
  ├─ base.OnInspectorGUI()         ← disableOnPlaying 控制
  ├─ DrawConfigs()                 ← TypesSelector + 分隔线
  ├─ DrawPluginEntries()           ← SyncEntries + Drawer.Draw
  └─ FinalRefreshInspectorGUI()   ← 提交序列化修改

OnDisable
  └─ m_Drawer?.Dispose()          ← 清理 Foldout 缓存，置 null
```

> Play 模式下，`disableOnPlaying=true`（继承自 `BaseComponentInspector`）自动禁用所有控件，不展示运行时数据面板。

---

## §8 初始化时序

`OnEnable` 阶段 `PluginEntriesDrawer.SyncEntries` 已执行一次完整的反射扫描与条目同步，
后续每帧 `DrawPluginEntries` 仍调用 `SyncEntries`，确保编译刷新后新增类型即时出现。

---

## §11 使用示例

```csharp
// Inspector 无需手动使用，Unity 编辑器自动调用。
// 开发者仅需在 SDKComponent 上挂载插件并启用对应条目。

// Inspector 展示结构示意：
// [SDK 管理器]  ▼ SDKManager
// ─────────────────────────────
// ▶ 埋点 (2)
//     ☑ MyTrackPlugin  [Track]    100
//     ☐ AnotherPlugin  [Attribution]  100
// ▶ 变现 (1)
//     ☑ MyAdPlugin  [Ad]    100
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
