# ProcedureComponentInspector

**类签名**：`[CustomEditor(typeof(ProcedureComponent))] internal sealed partial class ProcedureComponentInspector : BaseComponentInspector`
**命名空间**：`NovaFramework.Editor`

ProcedureComponent 的 Inspector 定制面板，提供 Manager 实现类选择器、入口流程下拉选择器、启动阶段配置 Foldout、Play Mode 运行时只读状态展示（`ProcedureLoadDll` 反射读取 LoadException / LoadComplete / EntranceType + 当前流程）以及 Play Mode 流程跳转历史展示（历史数据来自 `ProcedureComponent.RunHistory`）。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `ProcedureComponentInspector.cs` | `sealed partial ProcedureComponentInspector` | 主体：`OnEnable`（绑定属性、反射缓存 ProcedureLoadDll internal 属性）、`OnInspectorGUI`、`RequiresConstantRepaint`、`OnCompileComplete` |
| `ProcedureComponentInspector.Visitors.cs` | `partial ProcedureComponentInspector` | 字段：Manager 类型名 / 入口流程 / LauncherSettings 属性；反射缓存 `m_LoadExceptionPropInfo` / `m_LoadCompletePropInfo` / `m_EntranceTypePropInfo`；`m_RunHistoryFoldout` |
| `ProcedureComponentInspector.Methods.cs` | `partial ProcedureComponentInspector` | 绘制方法：`DrawConfigs` / `DrawProcedureSettings` / `DrawLauncherSettings` / `DrawRuntimeInfo` / `DrawProcedureHistory`；反射辅助：`TryGetLoadException` / `TryGetLoadComplete` / `TryGetEntranceType`；`RefreshProcedureTypeNames` |

---

## §3 继承关系

```
UnityEditor.Editor
  └── BaseComponentInspector (abstract)
        └── ProcedureComponentInspector (sealed partial)
```

---

## §4 关键字段表

| 字段 | 类型 | 说明 |
|---|---|---|
| `m_CurManagerTypeName` | `SerializedProperty` | 绑定 `ProcedureComponent.m_CurManagerTypeName` |
| `m_ManagerTypeNames` | `List<string>` | 所有 `IProcedureManager` 实现类的类型名列表 |
| `m_EntranceProcedureTypeName` | `SerializedProperty` | 绑定 `ProcedureComponent.m_EntranceProcedureTypeName` |
| `m_LauncherSettings` | `SerializedProperty` | 绑定 `ProcedureComponent.m_LauncherSettings`（`LauncherSettings` 嵌套对象） |
| `m_ProcedureTypeNames` | `List<string>` | 所有 `ProcedureBase` 子类的类型名列表（自动扫描，含命名空间） |
| `m_RunHistoryFoldout` | `bool` | Procedure History Foldout 展开状态；默认 `true` |
| `m_LoadExceptionPropInfo` | `System.Reflection.PropertyInfo` | `OnEnable` 中反射缓存的 `ProcedureLoadDll.LoadException` 属性信息 |
| `m_LoadCompletePropInfo` | `System.Reflection.PropertyInfo` | 反射缓存的 `ProcedureLoadDll.LoadComplete` 属性信息 |
| `m_EntranceTypePropInfo` | `System.Reflection.PropertyInfo` | 反射缓存的 `ProcedureLoadDll.EntranceType` 属性信息 |

---

## §5 完整公开 API

```csharp
// --- 覆写 BaseComponentInspector ---
protected override void OnEnable()
public override void OnInspectorGUI()
public override bool RequiresConstantRepaint()   // Play Mode 下返回 true，驱动每帧重绘
protected override void OnCompileComplete()

// --- 私有方法 ---
private void DrawConfigs()                        // 绘制 Manager 类型选择器
private void DrawProcedureSettings()              // 绘制入口流程选择器
private void RefreshProcedureTypeNames()          // 刷新 m_ProcedureTypeNames + 验证入口流程有效性
private void DrawLauncherSettings()               // 绘制「启动阶段配置」Foldout
private void DrawRuntimeInfo()                    // 绘制运行时只读状态（仅 Play Mode）
private void DrawProcedureHistory()               // 绘制 Procedure History Foldout（仅 Play Mode）

// --- 反射辅助 ---
private Exception TryGetLoadException(ProcedureBase procedure)   // 反射读取 ProcedureLoadDll.LoadException
private bool? TryGetLoadComplete(ProcedureBase procedure)        // 反射读取 ProcedureLoadDll.LoadComplete
private Type TryGetEntranceType(ProcedureBase procedure)         // 反射读取 ProcedureLoadDll.EntranceType
```

---

## §9 关键算法

### Play Mode 跳转历史展示机制

历史数据**存储在 `ProcedureComponent.RunHistory`（Runtime 侧），Inspector 只做只读展示**：

1. `RequiresConstantRepaint()` 在 `EditorApplication.isPlaying` 时返回 `true`，驱动每帧重绘。
2. `DrawProcedureHistory()` 直接读取 `((ProcedureComponent)target).RunHistory`（`IReadOnlyList<ProcedureRunInfo>`）并渲染列表，不在 Inspector 侧维护任何历史状态。
3. 每条记录展示 `#N */{TypeFullName}` + `hh:mm:ss` 时长；`*` 表示当前活跃条目。

> 历史采集已在 W8 版本下沉至 `ProcedureComponent`（`#if UNITY_EDITOR` 条件编译），解决了 AssetBundle 启动期 Inspector 实例重建导致历史清零的问题。

### Inspector 反射获取 internal 属性

`ProcedureLoadDll.LoadComplete` / `LoadException` / `EntranceType` 为 `internal` 属性，跨 assembly 须反射访问。`OnEnable` 中通过 `typeof(ProcedureBase).Assembly.GetType("NovaFramework.Runtime.ProcedureLoadDll")` 定位类型，再用 `BindingFlags.Instance | NonPublic | Public` 获取并缓存 `PropertyInfo`；`TryGet*` 辅助方法在调用前检查类型全名匹配，防止非 ProcedureLoadDll 实例误反射。

---

## §11 使用示例

Inspector 面板自动出现在 `ProcedureComponent` 的 Inspector 中，无需额外调用。

面板内容（上到下）：

1. **Procedure 管理器**：类型选择器（下拉选择 `IProcedureManager` 实现类）
2. **入口流程**：下拉选择器，候选列表为自动扫描到的所有 `ProcedureBase` 子类
3. **启动阶段配置**（Foldout「启动阶段配置」）：`LauncherSettings` 嵌套字段（闪屏时长 + 三个 Prefab 名称 + `LocalizationJsonPathTemplate`）
4. **运行时状态**（仅 Play Mode）：
   - `LoadException` 非 null 时：`HelpBox(MessageType.Error, exception.Message)`
   - 当前流程：只读标签，显示当前正在执行的流程全限定类名
   - `LoadComplete` / `EntranceType`：仅当 CurrentProcedure 为 `ProcedureLoadDll` 时展示
5. **Procedure History**（Foldout，仅 Play Mode，默认展开）：
   - 每条：`#N */{TypeFullName}  hh:mm:ss`
   - 活跃条目时长因 `RequiresConstantRepaint` 实时累积；停止 Play Mode 后清空

---

## §12 注意事项

- 跳转历史数据存储在 `ProcedureComponent`（Runtime 侧），不跨 Domain Reload 持久化（仅 Editor 构建存在此数据，发布构建完全移除）。
- 面板未选中 ProcedureComponent 时不刷新历史（`RequiresConstantRepaint` 只在目标面板生效）——此盲区已接受。
- 同帧多切漏记（ProcedureComponent.Update 每帧轮询，同帧内多次切换只记最终状态）——此盲区已接受。

---

## §13 关联文档

- [ProcedureComponent.md](../../../Runtime/Modules/Procedure/ProcedureComponent.md)（RunHistory 数据源）
- [DllAssetEntry.md](../../../Runtime/Modules/Config/Definitions/DllAssetEntry.md)
- [LauncherSettings.md](../../../Runtime/Modules/Procedure/LauncherSettings.md)
- [ProcedureBase.md](../../../Runtime/Modules/Procedure/ProcedureBase.md)
- [BaseComponentInspector.md](../BaseComponentInspector.md)
- [EditorUtil.Draw.md](../../EditorUtil/EditorUtil.Draw/EditorUtil.Draw.md)
