# SDKComponentInspector

**类签名**：`[CustomEditor(typeof(SDKComponent))] internal sealed partial class SDKComponentInspector : BaseComponentInspector`  
**命名空间**：`NovaFramework.Editor`  
**目标组件**：`NovaFramework.Runtime.SDKComponent`

SDK 模块 Inspector，负责绘制 `ISDKManager` 类型选择器、SDK Plugin 条目分组列表和底部打点工具区。Plugin 条目可见性由当前 active `ConfigMaster.EnabledSDKs` 决定。

---

## 文件表

| 文件 | 类型 | 说明 |
|------|------|------|
| `SDKComponentInspector.cs` | `SDKComponentInspector` | 主体：`OnEnable` 绑定属性、初始化 Drawer、订阅 Config 保存事件，`OnInspectorGUI` 调度绘制 |
| `SDKComponentInspector.Visitors.cs` | `SDKComponentInspector` | `SerializedProperty` 字段与 Drawer 引用声明 |
| `SDKComponentInspector.Methods.cs` | `SDKComponentInspector` | 私有绘制方法：`DrawConfigs`、`DrawPluginEntries` |
| `SDKComponentInspector.PluginEntriesDrawer.cs` | `SDKComponentInspector.PluginEntriesDrawer` | 嵌套绘制器：active ConfigMaster 过滤、分组渲染、Missing 检测与清理 |

---

## 绘制结构

```text
OnInspectorGUI
  base.OnInspectorGUI()
  DrawConfigs()
    SDK 管理器类型选择器
    SDK 管理器说明
    分隔线
  DrawPluginEntries()
    SyncEntries
    Drawer.Draw
  DrawTrackTools()
    底部打点工具区
    打开打点表按钮
  FinalRefreshInspectorGUI()
```

“打开打点表”按钮位于 Inspector 底部独立“打点工具”区域，会按当前工程所有 `Tracks.xlsx` 直接生成 `Library/Nova/Tracks/Tracks.generated.xlsx`，生成成功后再打开 Excel。生成逻辑由 `EditorUtil.TrackRegistry` 承担，扫描路径按固定优先级追加：`Assets/Framework/Tracks/Tracks.xlsx` → `com.solotopia.nova.framework/Tracks/Tracks.xlsx` → `UPMPackages/*/Nova/Tracks/Tracks.xlsx` → `Packages/*/Nova/Tracks/Tracks.xlsx`。导出 Sheet 名严格沿用源 Excel 的 Sheet 名，重名时直接失败并提示冲突来源。

---

## 关键字段

| 字段 | 类型 | 说明 |
|------|------|------|
| `m_CurManagerTypeName` | `SerializedProperty` | 绑定 `SDKComponent.m_CurManagerTypeName` |
| `m_PluginEntries` | `SerializedProperty` | 绑定 `SDKComponent.m_PluginEntries` |
| `m_ManagerTypeNames` | `List<string>` | `ISDKManager` 所有实现类型名称，供下拉选择 |
| `m_Drawer` | `PluginEntriesDrawer` | Plugin 条目列表绘制器 |

---

## 行为语义

- Manager 下拉仍扫描所有 `ISDKManager` 实现。
- Plugin 分组只显示当前 active `ConfigMaster.EnabledSDKs` 映射出的 Plugin。
- `EnabledSDKs` 存的是 `ISDKPluginConfig` 类型 FullName，Drawer 通过泛型基类或 `SDKPluginConfigTypeAttribute` 静态映射到 Plugin 类型，不构造候选 Plugin。
- ConfigMaster 中未启用的旧 `m_PluginEntries` 条目会保留，但不参与显示、默认启用判断或单选写回。
- Missing 区域只显示类型已经无法解析的 Entry，不显示 inactive Entry。
- 底部打点工具只负责生成并打开本地汇总表，不修改 `SDKComponent` 的序列化字段。

---

## 生命周期

```text
OnEnable
  base.OnEnable()
  绑定 m_CurManagerTypeName / m_PluginEntries
  收集 ISDKManager 实现类型名称
  创建 PluginEntriesDrawer
  初始同步 PluginEntries
  订阅 EditorUtil.Config.Events.ActiveConfigMasterSaved

OnActiveConfigMasterSaved
  serializedObject.Update()
  m_Drawer.ForceRefresh()
  m_Drawer.SyncEntries(...)
  Repaint()

OnDisable
  取消订阅 Config 保存事件
  释放 PluginEntriesDrawer
```

---

## 关联文档

- [PluginEntriesDrawer.md](./PluginEntriesDrawer.md)
- [BaseComponentInspector.md](../BaseComponentInspector.md)
- [SDKComponent.md](../../../Runtime/Modules/SDK/SDKComponent.md)
- [Definitions/SDKPluginEntry.md](../../../Runtime/Modules/SDK/Definitions/SDKPluginEntry.md)
- [Managers/Interfaces/ISDKManager.md](../../../Runtime/Modules/SDK/Managers/Interfaces/ISDKManager.md)
- [EditorUtil.Config.WorkspaceActive.md](../../EditorUtil/EditorUtil.Config/EditorUtil.Config.WorkspaceActive.md)
