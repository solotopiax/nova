# EditorUtil.Draw

**类签名**：`public static partial class EditorUtil { public static partial class Draw { ... } }`
**命名空间**：`NovaFramework.Editor`

Inspector GUI 绘制工具集，封装 `EditorGUILayout` / `GUILayout` 并统一支持 `disableOnPlaying`（运行时禁用）参数。

> **强制约定**：所有 Framework Inspector 的绘制必须使用 `EditorUtil.Draw.*`，禁止直接调用 `EditorGUILayout.*` / `GUILayout.*`。

---

## 文件

| 文件 | 提供的方法组 |
|------|------------|
| `EditorUtil.Draw.Button.cs` | `Draw.Button` / `Draw.LinkButton` |
| `EditorUtil.Draw.Foldout.cs` | `Draw.Foldout` / `Draw.CleanFoldout` |
| `EditorUtil.Draw.HelpBox.cs` | `Draw.HelpBox` |
| `EditorUtil.Draw.Layout.cs` | `Draw.Layout.*` |
| `EditorUtil.Draw.Property.cs` | `Draw.Label` / `Draw.LabelInline` / `Draw.TextField` / `Draw.DelayedTextField` / `Draw.Property` / `Draw.IntField` / `Draw.IntSlider` / `Draw.FloatSlider` / `Draw.Toggle` / `Draw.ToggleLeft` / `Draw.ToggleInline` / `Draw.EnumSelector` / `Draw.EnumPopup` / `Draw.TypesSelector` / `Draw.FloatSelectionGrid` / `Draw.SelectableLabel` |
| `EditorUtil.Draw.SourceFileTree.cs` | `Draw.SourceFileTree.*`（数据源文件树绘制 + 命名空间列表编辑，见 [EditorUtil.Draw.SourceFileTree.md](EditorUtil.Draw.SourceFileTree.md)） |
| `EditorUtil.Draw.Toolbar.cs` | `Draw.Toolbar` |

---

## Draw.Layout — 布局块

```csharp
// 水平/垂直布局块（Action 内绘制子控件，自动 Begin/End，异常安全）
Draw.Layout.Horizontal(Action draw, params GUILayoutOption[] options)
Draw.Layout.Horizontal(GUIStyle style, Action draw, params GUILayoutOption[] options)
Draw.Layout.Horizontal(string styleName, Action draw, params GUILayoutOption[] options)

Draw.Layout.Vertical(Action draw, params GUILayoutOption[] options)
Draw.Layout.Vertical(GUIStyle style, Action draw, params GUILayoutOption[] options)
Draw.Layout.Vertical(string styleName, Action draw, params GUILayoutOption[] options)
```

---

## Draw.Button — 按钮

```csharp
// 普通按钮（disableOnPlaying=true 时运行期间灰显不可点击）
void Draw.Button(string label, bool disableOnPlaying = true, Action onClick = null, params GUILayoutOption[] options)
void Draw.Button(string label, float width, bool disableOnPlaying = true, Action onClick = null, params GUILayoutOption[] options)

// 超链接样式按钮（点击打开 URL）
void Draw.LinkButton(string url, string label = null)
```

程序集内另有 `LinkButton(Rect rect, string url, string label = null)`，用于 ReorderableList 等手动布局；保留下划线与链接光标，点击后打开浏览器并继续当前 GUI 绘制。

`InvalidBorder(Rect rect)` 为程序集内手动布局输入框绘制红色描边，仅在 Repaint 阶段绘制，不改变 GUI 变化检测状态。

---

## Draw.Foldout — 折叠块

```csharp
// 内部持久化状态版（key 唯一标识折叠项，跨 Inspector 刷新保持状态）
bool Draw.Foldout(string displayName, string listName = null, bool defaultOpen = false)

// 外部管理状态版（Layout 自动分配 Rect）
bool Draw.Foldout(ref bool foldout, string displayName, bool toggleOnLabelClick = true)
bool Draw.Foldout(ref bool foldout, string displayName, bool toggleOnLabelClick, GUIStyle style, params GUILayoutOption[] options)
bool Draw.Foldout(ref bool foldout, string displayName, bool toggleOnLabelClick, params GUILayoutOption[] options)

// 外部管理状态版（手动指定 Rect，用于 TreeView / PropertyDrawer 等自定义布局）
bool Draw.Foldout(Rect rect, ref bool foldout, string displayName, bool toggleOnLabelClick, GUIStyle style)

// 清除指定 key 的缓存状态
void Draw.CleanFoldout(string listName)

// 清除所有折叠状态缓存
void Draw.ClearAllFoldoutCache()
```

---

## Draw.Property — 字段绘制

```csharp
// 标签文本
void Draw.Label(string text, bool disableOnPlaying = true, params GUILayoutOption[] options)
void Draw.Label(string text, string content, bool disableOnPlaying = true)
void Draw.Label(string text, GUIStyle style, bool disableOnPlaying = true, params GUILayoutOption[] options)

// 内联紧凑标签（GUILayout.Label，按内容宽自适应、无 labelWidth 占位，用于同行多元素紧贴排版）
void Draw.LabelInline(string text, bool disableOnPlaying = true, params GUILayoutOption[] options)
void Draw.LabelInline(string text, GUIStyle style, bool disableOnPlaying = true, params GUILayoutOption[] options)

// 文本输入框
void Draw.TextField(SerializedProperty property, bool disableOnPlaying = true,
    Action onComplete = null, params GUILayoutOption[] options)
void Draw.TextField(SerializedProperty property, GUIStyle style,
    bool disableOnPlaying = true, Action onComplete = null, params GUILayoutOption[] options)

// 延迟提交文本框（仅 Enter/失焦时返回新值，编辑过程中返回旧值；规避 IMGUI 聚焦态内部缓冲冲突）
string Draw.DelayedTextField(string value, bool disableOnPlaying = true, params GUILayoutOption[] options)

// 通用 SerializedProperty 绘制
void Draw.Property(SerializedProperty property, bool disableOnPlaying = true, params GUILayoutOption[] options)
void Draw.Property(string label, SerializedProperty property, bool disableOnPlaying = true, params GUILayoutOption[] options)

// 整数输入框（只读展示，不写回 SerializedProperty）
int Draw.IntField(string label, int value, bool disableOnPlaying = true, params GUILayoutOption[] options)

// 整数滑杆（同步写回 SerializedProperty，运行时通过 runtimeSetter 实时生效）
void Draw.IntSlider(string label, SerializedProperty property, int min, int max,
    bool disableOnPlaying = true, Action<int> runtimeSetter = null, Action onComplete = null,
    params GUILayoutOption[] options)

// 浮点滑杆
void Draw.FloatSlider(string label, SerializedProperty property, float min, float max,
    bool disableOnPlaying = true, Action<float> runtimeSetter = null, Action onComplete = null,
    params GUILayoutOption[] options)

// 开关
void Draw.Toggle(string label, SerializedProperty property,
    bool disableOnPlaying = true, Action<bool> runtimeSetter = null, Action onComplete = null,
    params GUILayoutOption[] options)

// 左侧文本勾选（ToggleLeft，返回用户操作后的值）
bool Draw.ToggleLeft(string label, bool value, bool disableOnPlaying = true, params GUILayoutOption[] options)
bool Draw.ToggleLeft(string label, bool value, GUIStyle style, bool disableOnPlaying = true, params GUILayoutOption[] options)

// 内联紧凑勾选框（GUILayout.Toggle，checkbox 在左文字在右，按内容宽自适应、无 labelWidth 占位，用于同行多元素紧贴排版）
bool Draw.ToggleInline(string label, bool value, bool disableOnPlaying = true, params GUILayoutOption[] options)

// 枚举下拉（带标签）
void Draw.EnumSelector<TEnum>(string label, SerializedProperty property,
    bool disableOnPlaying = true, params GUILayoutOption[] options) where TEnum : Enum

// 枚举弹出（无标签，内联用）
void Draw.EnumPopup<TEnum>(SerializedProperty property, float width,
    bool disableOnPlaying = true, params GUILayoutOption[] options) where TEnum : Enum

// 类型名选择器（从 typeNames 列表中选择并写入 typeNameProperty）
void Draw.TypesSelector(string label, List<string> typeNames, SerializedProperty typeNameProperty,
    bool disableOnPlaying = true, params GUILayoutOption[] options)

// 可复制标签（不可编辑，可全选复制）
void Draw.SelectableLabel(string text, bool disableOnPlaying = true, params GUILayoutOption[] options)
void Draw.SelectableLabel(string text, GUIStyle style, bool disableOnPlaying = true, params GUILayoutOption[] options)

// 浮点值网格选择（将 SerializedProperty 值映射到网格按钮索引）
void Draw.FloatSelectionGrid(SerializedProperty property, string[] gridTexts,
    Func<float, int> valueToIndex, Func<int, float> indexToValue,
    bool disableOnPlaying = true, Action<float> runtimeSetter = null, params GUILayoutOption[] options)

// 暗黄小字说明标签（miniLabel，wordWrap 自动换行，暗黄色 #C79E33，用于为上方控件附加补充说明）
void Draw.HintLabel(string text, bool disableOnPlaying = true)

// 表格行（rect-based，与上方 LabelField 属性行像素级对齐）
// name         : 名称列文本，渲染在标签区（左侧），与属性行标签文本精确对齐
// columnTexts  : 各值列文本数组，渲染在值区（右侧），与属性行值文本精确对齐
// columnWidths : 各值列宽度数组（像素），与 columnTexts 一一对应
// 对齐原理：
//   Name 列用 EditorGUI.LabelField(rect)，其内部做 IndentedRect，文本位置与属性标签文本完全一致。
//   值列用 GUI.Label(rect, style)，不做 indent 偏移，文本位置与属性值文本完全一致。
//   分割点统一为 rowRect.x + EditorGUIUtility.labelWidth，与 PrefixLabel 的 valueRect.x 完全吻合。
void Draw.TableRow(string name, string[] columnTexts, float[] columnWidths, bool disableOnPlaying = false)
```

> **何时使用 `TableRow` vs `Horizontal + Label`**
>
> | 场景 | 使用方式 |
> |---|---|
> | 表格行需要与上方 `Label(label, value)` 属性行**列对齐** | `Draw.TableRow` |
> | 纯水平排列控件，无需与属性行对齐 | `Draw.Layout.Horizontal + Draw.Label(text, false, GUILayout.Width(w))` |

---

## Draw.Toolbar — 工具栏

```csharp
// 标签页式工具栏（写回 SerializedProperty）
void Draw.Toolbar(string[] titles, SerializedProperty property,
    bool disableOnPlaying = true, Action<int> runtimeSetter = null, params GUILayoutOption[] options)
void Draw.Toolbar(string[] titles, SerializedProperty property, float height = 20f,
    bool disableOnPlaying = true, Action<int> runtimeSetter = null)
```

---

## Draw.HelpBox — 提示框

```csharp
// 支持多行 messages，自定义 MessageType（Info / Warning / Error）
// 使用 Unity 内置图标（console.infoicon.sml / console.warnicon.sml / console.erroricon.sml）区分类型
// GUILayoutOption 合并使用固定长度数组避免 GC 分配
void Draw.HelpBox(MessageType messageType, string[] messages,
    bool disableOnPlaying = true, params GUILayoutOption[] options)
```

---

## Draw.Panel — 文件/目录选择

```csharp
void Draw.Panel.SelectFile(string label, string extension, SerializedProperty property)
void Draw.Panel.SelectDirectory(string label, SerializedProperty property)
```

---

## disableOnPlaying 参数说明

| 值 | 行为 |
|----|------|
| `true`（默认） | 运行时该控件灰显、不可交互 |
| `false` | 运行时可交互（配合 `runtimeSetter` 实时写回运行时状态） |

---

## 关联文档

- [EditorUtil.md](../EditorUtil.md)
- [BaseComponentInspector.md](../../Inspectors/BaseComponentInspector.md)

## 交互、提交与异常的共同合同

Draw 是 GUI 工具，灰显只限制鼠标/键盘交互，不阻止绘制回调中的自动补项、归一或 Apply。普通值控件返回新值，由调用者保存；Property 会 Apply 整个 SerializedObject 待改内容，PropertyField 不 Apply。带 runtimeSetter 的 Toggle/滑杆/Toolbar 在 Edit 写序列化，Play 只调用 setter，setter 为 null 仍可能通知完成；Rect IntSlider 等无 setter 重载在允许 Play 时仍写序列化。

类型选择对未知/空值自动取首项，禁用态也可能补写并通知；缓存只看类型 List 的引用与 Count，等长改内容可能陈旧。枚举选择按 enumValueIndex 映射泛型 Enum.GetValues，未校实际枚举类型/索引；Flags 使用 intValue，超过 int32 可溢出。Float 网格不核映射委托/上界，绘制前的规范化也可能触发写入。PasswordField 只有视觉遮罩，存储仍明文。红框和 HelpBox 只显示，不是业务有效性或执行门。

标准布局块、DisabledGroup、ScrollView 与部分颜色控件用 finally 恢复；普通字段、HelpBox 布局版、源码树缩进等并非全部异常安全，回调或 ExitGUI 可打断收尾。默认 Button 点击回调后 ExitGUI，即使回调为空也会结束事件；Rect 版与显式 exitGUI=false 分支不同。LinkButton 直接 OpenURL 不校 scheme，Confirm 只是系统对话框，不等于 Action 授权 token。

Foldout key 在当前域全局共享，同 key 跨窗口联动；defaultOpen 只首次初始化，清全缓存影响所有调用者。GUIStyle 缓存是共享可变引用，不自动跟踪皮肤变化。全局缩进由调用者配对，工具不限制负值。

## 延迟路径与可重排列表

延迟路径面板捕获 SerializedProperty 引用，没有冻结目标生命周期或执行前复核；取消也通知回调。普通选择优先显式初始目录，其次有效历史/字段/工程根，允许保存工程外绝对路径。SelectFolderForFileDelay 保留的是登记时文件名，期间改字段可能被旧选择覆盖；SaveFileDelay 只选路径，不写文件。

ReorderableRowList 要求每帧 BeginFrame→DrawRow→EndFrame，索引为底层列表索引，过滤行可留下空隙；GUI.enabled/Play 不自动阻止点击，DraggableNow 只限制起拖。拖拽中数量变化不重核原索引，回调异常可在 Reset/Use 前留下 hotControl；调用者应在异常出口 ResetDragState 并校验重排范围。
