---
id: PAT-149
title: IMGUI Foldout 展开位移先查布局 margin 传播
type: pattern
status: active
date: 2026-07-28
summary: Foldout 位移先量 Rect 并阻断 margin 传播
category: inspector
aliases:
  - PAT-149-imgui-foldout-margin-propagation-layout-shift
  - IMGUI Foldout 展开位移排障
keywords:
  - PAT-149
  - IMGUI Foldout
  - margin 传播
  - 展开位移
  - GetControlRect
  - Inspector 滚动条
tags:
  - pattern
  - editor
  - inspector
  - imgui
  - debugging
related:
  - "[[PAT-24-inspector-row-vertical-alignment|PAT-24]]"
  - "[[PAT-39-editor-draw-discipline-enforcement|PAT-39]]"
  - "[[PAT-46-iteration-grep-self-check|PAT-46]]"
  - "[[PAT-136-symptom-driven-debug-trap|PAT-136]]"
---

# PAT-149：IMGUI Foldout 展开位移先查布局 margin 传播

## 适用场景

- Inspector 中的 Foldout 收起与展开时，标题、箭头或同级内容发生轻微横向位移。
- 只有首个可展开区域展开时发生位移，再展开其他区域不再继续移动。
- 展开后内容高度超过 Inspector，可观察到垂直滚动条首次出现。
- 多次调整箭头 Rect、缩进宽度或局部像素后，问题仍然存在或转移到其他层级。

## 核心结论

Foldout 的箭头方向变化不一定是位移源。IMGUI 自动布局会综合子布局的 `margin` 计算父级布局边界；当收起态不存在带样式子容器、展开态首次出现 `box` 等带 margin 的容器时，子容器的 margin 可能向上传播，改变父级内容的左起点。

垂直滚动条通常只是触发宽度重算的条件，不等于根因。正确修复应让承担树形缩进的布局组拥有稳定、明确的零 margin 样式，从布局源头阻断状态相关的 margin 传播；不要根据展开状态添加负像素补偿。

## 排障方法

### 1. 先固定复现状态

至少比较三种状态：

1. 所有目标 Foldout 收起。
2. 首次展开一个会显著增加高度的 Foldout。
3. 保持一个 Foldout 展开，再展开另一个 Foldout。

如果只有第二种状态发生位移，应优先检查“首次新增子布局”与“首次出现滚动条”共同触发的布局重算。

### 2. 记录真实 Rect，不凭截图猜箭头

在 `EventType.Repaint` 阶段临时记录关键行的 `EditorGUILayout.GetControlRect()` 结果，并同时记录 `EditorGUIUtility.currentViewWidth`。按层级选取代表项：顶层标题、工程标题、工程子标题和展开后的叶子标题。

判断规则：

- `x` 改变：该行或祖先布局的左边界发生变化。
- `x` 不变、`width` 变小：通常只是滚动条占用了可用宽度。
- 顶层不动、某个子层整体移动：根因位于该子层共同祖先布局，不在顶层 Inspector。
- 箭头、标题和右侧按钮一起移动：不要继续单独修箭头 Rect。

### 3. 一次只验证一个假设

推荐顺序：

1. 验证是否只是滚动条缩窄宽度。
2. 验证缩进容器是否存在状态相关的最小宽度。
3. 验证展开内容的 `box` 或其他 GUIStyle margin 是否向父级传播。
4. 用零 margin 的父级布局样式切断传播，再重复三态测量。

每个假设都必须有修复前后的坐标证据。未改变坐标的尝试应立即撤销，不能继续叠加。

## 正确实现原则

- 所有业务 Foldout 统一代理到 `EditorUtil.Draw` 的单一内部绘制入口。
- 箭头、可选 Toggle、标题和右侧按钮使用固定槽位；展开状态只改变状态值，不改变槽位几何。
- 树形缩进由公共布局容器表达，不由业务 Inspector 散落调用 `EditorGUI.IndentedRect`。
- 缩进布局的内容容器显式使用零 margin 样式，避免展开内容的样式反向控制父级起点。
- 每级缩进使用固定正向间距；禁止用 `-3px`、`row.x - N` 等状态补偿掩盖布局传播。

## 经验证的判据

Table Inspector 的复现中，展开前工程子标题的真实横坐标为 `x=51`，首次展开导出描述并出现滚动条后变为 `x=54`；顶层与工程标题保持不变，证明问题位于工程内部共同缩进容器。

为缩进内容容器设置零 margin 样式后：

- 收起状态：工程子标题 `x=50`。
- 首次展开导出描述：工程子标题仍为 `x=50`。
- 展开 Excel 清单：工程子标题仍为 `x=50`。
- 父子层级依次稳定为 `18 → 36 → 50 → 61`，滚动条只改变宽度，不再改变左起点。

验证来源：

- `Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.Draw/EditorUtil.Draw.Layout.cs`
- `Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.Draw/EditorUtil.Draw.Foldout.cs`
- `Assets/Framework/Scripts/Editor/Inspectors/TableComponentInspector/TableComponentInspector.Methods.cs`
- Unity 6.4 Inspector 的收起、展开导出描述、展开 Excel 清单三态实测。

## 深度反省

### 错误一：把视觉症状直接归因于箭头实现

看到 Foldout 展开时横移，最容易直接改箭头 Rect、`indentLevel` 或标题偏移。但当同级字段和父区域一起移动时，箭头只是跟随布局结果绘制，不是布局输入。未先区分“单控件移动”和“祖先布局移动”，会让修复长期停留在症状层。

### 错误二：静态代码推断代替现场坐标证据

IMGUI 的宽度、margin 合并和滚动视图行为由 Layout/Repaint 两阶段共同决定，仅阅读代码很难确认最终 Rect。连续凭感觉调整缩进与宽度，会产生看似合理但无法解释全部复现规律的假设。视觉 bug 必须把最终 Rect 当作事实源。

### 错误三：无效尝试没有立即撤销

给布局添加 `MinWidth(0)` 是合理假设，但实测坐标仍为 `51 → 54`，说明它不是根因。此时必须撤销该尝试，再建立新假设。把多个未证实改动叠加在一起，会失去因果关系，并不断破坏已经正确的父子层级。

### 错误四：忽略“只有首次展开才移动”的强证据

“两个区域都收起时，首次展开任意一个才移动；已有一个展开后，再展开另一个不动”说明触发因素是某种首次进入布局树的共同结构，而不是每个 Foldout 自己的箭头。这个规律应当直接把排查范围提升到共同祖先布局和首次出现的样式容器。

### 错误五：没有尽早建立可重复的三态验证闭环

连续视觉迭代如果只依赖人工截图反馈，协作成本会迅速放大。应尽早通过反射或稳定交互固定 Foldout 状态，自动触发 Repaint，记录同一批关键 Rect，并为每个假设重复同一套三态测量。只有坐标稳定后，截图才用于最终视觉确认。

## 反模式

- 为展开态单独添加负像素或 `row.x - N` 补偿。
- 为不同业务 Foldout 建立多套几何实现。
- 只看箭头截图，不记录标题行与祖先层级的真实 Rect。
- 看到滚动条出现就断言滚动条是根因。
- 一个假设失败后保留改动，再叠加第二个猜测。
- 只验证一种展开状态，未覆盖首次展开和已有展开两条路径。

## 关联

- [[PAT-39-editor-draw-discipline-enforcement|PAT-39]]：缺少布局能力时先补公共 Draw，不在业务 Inspector 建旁路。
- [[PAT-46-iteration-grep-self-check|PAT-46]]：每轮视觉修复后检查是否重新扩散原生绘制。
- [[PAT-136-symptom-driven-debug-trap|PAT-136]]：同源问题应沿共同状态链排查，不逐个症状补丁。
- [[PAT-24-inspector-row-vertical-alignment|PAT-24]]：稳定层级和左起点是同层编辑区对齐的前提。
