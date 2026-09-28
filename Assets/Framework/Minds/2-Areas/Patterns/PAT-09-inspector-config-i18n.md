---
id: PAT-09
title: Inspector 配置分组与中文说明模式
type: pattern
status: active
date: 2026-06-05
summary: Inspector 配置按业务分组并配中文说明
category: inspector
aliases:
  - PAT-09-inspector-config-i18n
keywords:
  - PAT-09
  - Inspector 分组
  - Inspector 中文说明
tags:
  - pattern
  - editor
  - inspector
  - nova
related:
  - "[[PAT-21-inspector-helpbox-multiline|PAT-21]]"
  - "[[PAT-24-inspector-row-vertical-alignment|PAT-24]]"
  - "[[PAT-35-editor-draw-only|PAT-35]]"
  - "[[PAT-46-iteration-grep-self-check|PAT-46]]"
---

# PAT-09：Inspector 配置分组与中文说明模式

## 适用场景

- `Assets/Framework/Scripts/Editor/Inspectors/**` 下的 `*ComponentInspector`
- `ConfigWindow` 等在同一面板内绘制多个配置类别的 EditorWindow
- 一个配置区包含多个业务簇，直接平铺会明显增加阅读成本
- 需要把字段语义、默认行为、推荐值、边界条件直接暴露给面板使用者

## 核心规则

### 1. 先按业务簇分组，再决定是否折叠

- 同一业务范畴下至少有 2 个字段时，才考虑单独建一个 Foldout。
- 单字段语义簇保持顶层平铺，不要为了“看起来整齐”强行建 Foldout。
- 分组的目标是降低理解成本，不是制造层级。

### 2. 字段说明必须中文化，且紧贴字段

- 面向人的 Label、HelpBox、Tooltip 统一写中文。
- 一个字段需要说明时，HelpBox 紧跟该字段下方，不要把说明挪到面板其他区域。
- 把“同类别字段 → 本类别 HelpBox”视作不可拆的视觉单元；一个单元结束后再画下一类别。不要先集中画完不同类别的输入框，再集中堆叠说明。
- 说明写“作用 + 默认行为/推荐值 + 关键边界”，不要写空泛描述。

### 2.1. 输入控件尺寸服从数据形态

- JSON、长文本等天然需要多行阅读和编辑的字段，即使当前为空或某平台下禁用，也应保留多行输入形态；不能仅凭空白面积擅自缩成单行。
- 调整尺寸前先核对实际数据长度及使用方式，避免把截图上的“空白”误判为无用空间。

### 3. 字段顺序与源码顺序保持一致

- Inspector 渲染顺序默认跟随配置类字段定义顺序。
- 如果要调整阅读优先级，应先调整配置类字段顺序，再同步 Inspector。
- 不要让“源码阅读顺序”和“面板阅读顺序”出现两套心智模型。

### 4. 层级缩进优先靠布局，不靠全局缩进状态

- 对“字段 + HelpBox”这类需要左缘对齐的内容，优先使用局部布局控制缩进。
- 不把 `indentLevel` 一类全局状态当成默认方案，否则字段和说明容易错位。

### 5. 同层级控件保持统一对齐宽度

- 同一层级下多行字段应使用统一的 label 宽度。
- 顶层与子层级可以不同，但同层级内部不要混用多套宽度。

## 当前落地状态

- `AssetComponentInspector`、`AppComponentInspector`、`ProcedureComponentInspector` 已较明显采用“中文 label + 字段后置说明 + 局部分组”的写法。
- 代码库里仍有部分旧 Inspector 保留历史缩进方式，因此本条应视为**当前主推荐模式**，不是“全库已 100% 统一”的事实描述。

## 反模式

- 每个字段都建一个 Foldout
- 只翻译字段名，不解释默认行为和边界
- 面板顺序与配置类顺序长期分裂
- 字段和对应 HelpBox 分属两处，或为了压缩空白把长 JSON 输入框改成单行
- 用全局缩进状态硬凑层级，导致字段与 HelpBox 错位

## 关联

- [[PAT-21-inspector-helpbox-multiline|PAT-21]]：多条说明如何拆行
- [[PAT-24-inspector-row-vertical-alignment|PAT-24]]：同层级控件如何对齐
- [[PAT-35-editor-draw-only|PAT-35]]：绘制必须走 `EditorUtil.Draw`
- [[PAT-46-iteration-grep-self-check|PAT-46]]：每轮 Editor 改动都要核对视觉状态与绘制封装
