# Framework Editor

**程序集**：`NovaFramework.Editor.asmdef`  
**命名空间**：`NovaFramework.Editor`

## 子模块

| 目录 | 说明 |
|---|---|
| [`EditorUtil/`](EditorUtil/EditorUtil.md) | 编辑器工具与 Draw 封装 |
| [`Definitions/`](Definitions/IEditorRuntimeDrawer.md) | Editor 侧公共接口与数据结构 |
| [`DataPipeline/`](DataPipeline/DataPipeline.md) | 数据导出与预处理流水线 |
| [`Inspectors/`](Inspectors/Inspectors.md) | 各 Runtime 组件的自定义 Inspector |
| [`Menus/`](Menus/Menus.md) | 菜单项 |
| [`Tools/`](Tools/Tools.md) | 非窗口型工具与处理器 |
| [`Windows/`](Windows/ConfigWindow.md) | `ConfigWindow`、`PipifyWindow`、`CheckUpdateWindow`、`PlugPalsWindow`、`KitsViewWindow` |

## 当前 Inspector 模型

### BaseComponentInspector 的职责

`BaseComponentInspector` 当前只负责这些基础能力：

- 统一设置与恢复 `EditorGUIUtility.labelWidth`
- 处理编译开始/结束状态回调
- 在 `OnInspectorGUI()` 开头执行 `serializedObject.Update()`
- 提供 `FinalRefreshInspectorGUI()` 统一 `ApplyModifiedProperties()` 与 `Repaint()`
- 提供模板路径相关辅助方法

它**不**持有统一的 RuntimeDrawer 列表，也**不**在基类里统一调度 `IEditorRuntimeDrawer`。

### IEditorRuntimeDrawer 的实际位置

`IEditorRuntimeDrawer` 是一个**可选扩展接口**：

- 接口定义位于 `Editor/Definitions/IEditorRuntimeDrawer.cs`
- 具体 Inspector 可以自行维护 `List<IEditorRuntimeDrawer>`
- 当前 `EventComponentInspector` 就采用了这种模式
- 是否使用、何时调用，由具体 Inspector 决定，不是 `BaseComponentInspector` 的基类契约

## 典型 Inspector 流程

当前常见写法是：

1. `OnEnable()` 里绑定 `SerializedProperty`
2. `base.OnInspectorGUI()` 做基础准备
3. 子类调用自己的 `DrawConfigs()` / `DrawRuntimeInfos()` / `DrawRuntimeLists()`
4. 最后调用 `FinalRefreshInspectorGUI()`

## Editor 侧几个当前事实

- `PlugPalsWindow` 属于 `Windows/`，不是 `Tools/`
- `KitsViewWindow` 属于 `Windows/`
- 业务侧 Editor UI 以 `EditorUtil.Draw` 为主，不鼓励在 Inspector/Window 里直接扩散原生 IMGUI 写法

## 阅读顺序建议

- 要看面板或窗口行为：先读对应 `Inspectors/` 或 `Windows/` 文档
- 要看共通 UI 封装：读 `EditorUtil.Draw`
- 要看 Runtime 与 Editor 的连接点：读组件对应的 Inspector 文档
