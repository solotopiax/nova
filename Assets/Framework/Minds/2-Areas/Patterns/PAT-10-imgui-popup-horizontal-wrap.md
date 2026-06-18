---
id: PAT-10
title: IMGUI 自定义 Popup 必须 Layout.Horizontal 包裹 Label + 控件
type: pattern
status: active
date: 2026-05-16
summary: IMGUI Popup需横向包裹避免错位抖动
category: inspector
aliases:
  - PAT-10-imgui-popup-horizontal-wrap
keywords:
  - PAT-10
  - PAT-10-imgui-popup-horizontal-wrap
tags:
  - pattern
  - editor
  - imgui
  - inspector
  - ux
  - nova
related:
  - "[[PAT-09-inspector-config-i18n|PAT-09]]"
  - "[[PAT-04-read-what-you-change|PAT-04]]"
  - "[[ADR-014-playmode-split-editor-runtime|ADR-014]]"
---

# PAT-10：IMGUI 自定义 Popup 必须 Layout.Horizontal 包裹 Label + 控件

## 适用场景（When）

- 在 Inspector / EditorWindow 中需要 `IntPopup` / `Popup` 等**限制选项集**控件
- `EditorUtil.Draw` 没有对应封装（如本项目 `EditorUtil.Draw.EnumPopup` 只支持完整枚举，无 IntPopup 限制选项集变体）
- 必须**回退裸 `EditorGUILayout.IntPopup` / `EditorGUILayout.Popup`** 实现
- 同时该控件需要：①与同层级其他 `EditorUtil.Draw.Property(...)` 字段视觉对齐 ②触发 `BeginChangeCheck` 联动其他字段

## 核心做法（What & How）

### 一、铁律：Label + Popup 必须用 Layout.Horizontal 包裹同一行

**根因：** `EditorUtil.Draw.Label` 与 `EditorGUILayout.IntPopup`（裸 IMGUI）默认在 IMGUI 自动布局模式下**各占一行**——`Label` 渲染完即换行，`IntPopup` 落到下一行。视觉上呈：

```
终端加载模式：       ← Label 占一行
[HostPlayMode ▼]    ← IntPopup 占下一行
```

而旁边的 `EditorUtil.Draw.Property("编辑器加载模式：", prop, ...)` 是同行渲染的，两者排版风格不一致，破坏 Inspector 视觉对齐。

### 二、正确范式

```csharp
private void DrawRuntimePlayModePopup()
{
    int curValue = m_RuntimePlayMode.intValue;
    int[]    optionValues = { (int)A, (int)B, (int)C };
    string[] optionLabels = { "A", "B", "C" };

    int  newValue = curValue;
    bool changed  = false;

    // Label + IntPopup 同行渲染（Horizontal 包裹）
    EditorUtil.Draw.Layout.Horizontal(() =>
    {
        EditorUtil.Draw.Label("终端加载模式：", false, GUILayout.Width(180f));
        EditorGUI.BeginChangeCheck();
        newValue = EditorGUILayout.IntPopup(curValue, optionLabels, optionValues);
        changed  = EditorGUI.EndChangeCheck();
    });

    if (changed)
    {
        m_RuntimePlayMode.intValue = newValue;
        // ... 联动逻辑
        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();
    }
}
```

### 三、关键约束

#### 3.1 BeginChangeCheck/EndChangeCheck 跨 lambda 闭包合法

`EditorUtil.Draw.Layout.Horizontal(Action drawAction)` 内部是 `try { BeginHorizontal; drawAction.Invoke(); } finally { EndHorizontal; }` 同步调用，**lambda 立即执行**（不是延迟回调）。

`EditorGUI.BeginChangeCheck()` / `EndChangeCheck()` 底层是 Unity 全局 `Stack<bool>`，配对依据是「**逻辑栈配对**」而非「C# 词法作用域」。lambda 内 `Begin` + lambda 内 `End` 完全合法，不会因为跨 lambda 边界出错。

#### 3.2 闭包变量必须在 lambda 外声明

`changed` / `newValue` 在 lambda **外**声明、lambda 内赋值、lambda 外读取。C# 闭包按引用捕获，外读拿到的就是 lambda 内写入的最新值。

```csharp
int  newValue = curValue;  // 必须 lambda 外声明
bool changed  = false;     // 必须 lambda 外声明
EditorUtil.Draw.Layout.Horizontal(() => {
    // lambda 内赋值给 newValue / changed
});
if (changed) { /* lambda 外读取 */ }
```

#### 3.3 Width(N) 与同层级 Property 对齐

Label 用 `GUILayout.Width(180f)` 锁定宽度，与同层级 `EditorUtil.Draw.Property("xxx：", prop, true, GUILayout.Width(180f))` 取相同值，保证：
- Label 列宽一致
- Popup/控件起点 X 一致
- 视觉上呈一条垂直线

参考 [[PAT-09-inspector-config-i18n|PAT-09]] §三·一「同层级条目右侧可编辑控件起点 X 必须上下对齐」。

#### 3.4 联动写回必须 Apply + Update 配对

```csharp
serializedObject.ApplyModifiedProperties();  // 持久化
serializedObject.Update();                    // 刷新让后续读到最新值
```

参考 [[PAT-09-inspector-config-i18n|PAT-09]] §九「双向联动落地范式」。

### 四、为什么不直接调 EditorGUILayout.BeginHorizontal/EndHorizontal

也可以——但本项目铁律是**Editor 侧禁用裸 `EditorGUILayout` / `GUILayout`**（详见 `csharp-code-style.md §四·一`），必须走 `EditorUtil.Draw.*` 封装。`EditorUtil.Draw.Layout.Horizontal(Action)` 是封装入口，回调式 API 必然走 lambda；闭包语义已论证合法，不存在替代方案的需求。

## 为什么这么做（Why）

- **视觉对齐是层级清晰度的硬指标**：同一区块内 Label 单行/双行混搭呈视觉锯齿，用户调参时眼睛要在控件之间跳跃
- **回退裸 IMGUI 是必然路径**：项目封装永远不可能覆盖所有 IMGUI 控件变体（IntPopup 限制选项集、自定义绘制 Rect 拖拽等），但回退必须遵守"和封装等价的视觉契约"
- **闭包恐惧症会导致绕路**：很多人看到"BeginChangeCheck 在 lambda 内"会以为不安全，改成扁平写法但绕开 Horizontal 封装，又回到顶端违规。固化"闭包合法"的认知可消除这种内耗

## 反模式（Anti-patterns）

- **裸调 Label + IntPopup 反模式**：`EditorUtil.Draw.Label("xxx", ...)` 紧跟一行 `EditorGUILayout.IntPopup(...)`，无 Horizontal 包裹。结果：Label 单独占一行、Popup 在下一行，视觉锯齿。修复：用 `EditorUtil.Draw.Layout.Horizontal(() => {...})` 包裹两者
- **回避 lambda 改用 BeginHorizontal/EndHorizontal 反模式**：因为"闭包不熟"绕开 `EditorUtil.Draw.Layout.Horizontal` 改成裸 `EditorGUILayout.BeginHorizontal()` + `EndHorizontal()`。结果：违反 Editor 侧封装铁律。修复：固化闭包合法认知，正面用回调式 API
- **lambda 内声明闭包变量反模式**：把 `changed` / `newValue` 写在 lambda 内 → lambda 外读不到。修复：变量必须 lambda 外声明
- **lambda 内直接走联动副作用反模式**：把 `m_RuntimePlayMode.intValue = newValue` + `ApplyModifiedProperties` 全塞 lambda 内。结果：副作用在 BeginHorizontal/EndHorizontal 之间执行，可能影响下一帧渲染状态。修复：lambda 内只做"采集 changed/newValue"，副作用在 lambda 外 `if (changed) { ... }` 块内
- **跨 lambda 调用栈失衡反模式**：lambda 内只 `BeginChangeCheck`，lambda 外 `EndChangeCheck`（或反过来）。结果：编译能过但 Unity 可能在某些代码路径下错位。修复：Begin 和 End 在同一 lambda 内成对
- **省略 Width(N) 反模式**：图省事不写 `GUILayout.Width(180f)`，让 Label 自适应。结果：Label 长度不同时控件起点 X 浮动，视觉锯齿。修复：所有同层级 label 锁定相同宽度

## 跨项目复用提示

- **能直接搬到任何 Unity 项目**：核心思想"自定义 Popup 必须用 Horizontal 包裹 Label + 控件 + Width 锁定 Label 宽度"通用
- **EditorUtil.Draw 等价物**：迁移到没有 EditorUtil.Draw 封装的项目时，把 `EditorUtil.Draw.Layout.Horizontal(Action)` 替换为 `EditorGUILayout.BeginHorizontal()` + `try { ... } finally { EditorGUILayout.EndHorizontal(); }`，把 `EditorUtil.Draw.Label` 替换为 `EditorGUILayout.LabelField`
- **非 IMGUI（UIToolkit/UI Builder）不适用**：UIToolkit 用 USS Flex 布局，无"控件默认占一行"的问题，本 PAT 仅针对 IMGUI

