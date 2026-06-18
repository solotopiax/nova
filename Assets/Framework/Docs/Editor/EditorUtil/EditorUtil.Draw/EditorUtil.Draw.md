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
