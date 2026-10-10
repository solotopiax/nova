---
id: PAT-20
title: Editor 配置详情页标题与缩进规则
type: pattern
status: active
date: 2026-06-05
summary: Editor详情页统一标题内容边界区域留白与紧凑操作层级
category: inspector
aliases:
  - PAT-20-editor-panel-title-indent
keywords:
  - PAT-20
  - Editor 配置详情页标题与缩进规则
  - PAT-20-editor-panel-title-indent
  - 文字左边缘等距
  - Foldout 层级缩进
tags:
  - pattern
  - editor
  - ui-style
related:
  - "[[PAT-09-inspector-config-i18n|PAT-09]]"
  - "[[PAT-21-inspector-helpbox-multiline|PAT-21]]"
  - "[[PAT-149-imgui-foldout-margin-propagation-layout-shift|PAT-149]]"
---

# PAT-20：Editor 配置详情页标题与缩进规则

## 适用场景

- `ConfigWindow` 一类右侧配置详情页
- Inspector 中存在明显分区的复杂配置块

## 核心规则

- 面板级配置区应有可读标题，不要直接把字段平铺到底
- 标题下的条目应有统一缩进，形成“标题 -> 内容”的视觉层级
- 只有在确实存在子分区时，才继续增加下一层缩进

### 树形层级按文字左边缘等距

- 父子层级的缩进基准是标题或条目文字的左边缘，不包含 Foldout 箭头占用的槽位。
- 相邻层级的文字起点统一错开 `11f`；该值对应 `EditorUtil.Draw.Layout.c_IndentPixelsPerLevel`，视觉上约为一个汉字宽度。
- `box` 等样式会附带不同的 `margin` 与 `padding`。业务页面应按最终文字起点补偿容器自带偏移，不能把同一个 `GUILayout.Space` 数值机械套到不同容器层级。
- 多级树至少同时核对三个相邻层级，确保一级到二级、二级到三级的文字左缘间距一致；箭头只表达展开状态，不参与层级距离判断。

### Config 详情页的平铺布局

- 复用同类页面的标题字号与样式，保证标题完整显示；不要用超大标题重复强调窗口名称。
- 页面内容保持统一左右边界，字段使用固定标签列；嵌套卡片扣除自身 padding 后仍须对齐同一值列，不靠取消留白把文字贴到边框。
- 功能区域用一致的留白和公共分隔线划分，避免层层套框。文字与边框、相邻行与按钮之间都应保留协调间距。
- 列表级操作放在标题行右侧，例如“现有密钥”右侧的“新增密钥”“显示密钥”；次要操作使用紧凑按钮，同一行按钮高度一致。
- 新增条目临时高亮，使待完成编辑与已有项可区分；保存后恢复普通状态。按钮使用用户理解的操作词，例如“手动输入”。
- 字段修改进入窗口统一脏状态与顶部保存，不重复增加面板专属保存、重读和提醒链。
- 树形缩进与平铺页面的左右留白是两种规则；具体尺寸按同类页面及共享绘制封装核对，不能把树形 `11f` 用作所有容器的统一 padding。

## 为什么这样定

- 复杂配置面板如果没有标题和缩进，阅读成本会迅速上升
- 统一层级语言后，跨面板切换时更容易定位当前正在配置的内容
- 以控件或容器边缘代替文字左缘，会把箭头槽和 Box 内边距误算为层级距离，导致相邻层级忽近忽远

## 反模式

- 复杂面板没有标题
- 每个区域各自定义一套缩进数值
- 直接把一个汉字理解为 `24f`，忽略 Editor 逻辑像素与 Retina 显示像素的缩放关系
- 只比较箭头或 Box 左缘，不比较实际文字左缘
- 不核对容器内边距，给每一级重复叠加相同的 `Space`
- 为了“看起来丰富”滥用多层嵌套
- 用很大的次要按钮挤占正文，或让每行按钮高度、字段值列各不相同
- 对齐边框却忽略框内文字与 padding，造成内容贴边

## 验证依据

- 标准层级宽度：`Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.Draw/EditorUtil.Draw.Layout.cs` 中的 `c_IndentPixelsPerLevel = 11f`。
- 容器位移排障与真实 Rect 判据：[[PAT-149-imgui-foldout-margin-propagation-layout-shift|PAT-149]]。
- 2026-08-13 Persist Inspector 的 PlayerPrefs、FileFragment、SQLite 三种存储实现在 Editor 与 Runtime 绘制路径中，按一级、分类、条目三级文字左缘统一间距。
- Config 平铺范式：`Assets/Framework/Scripts/Editor/Windows/ConfigWindow/ConfigWindow.RightPanel.ResourceEncryption.cs`；当前尺寸与全局保存边界见 `Assets/Framework/Docs/Editor/Windows/ConfigWindow.md` 的资源加解密配置章节。

## 关联

- [[PAT-09-inspector-config-i18n|PAT-09]]
- [[PAT-21-inspector-helpbox-multiline|PAT-21]]
- [[PAT-149-imgui-foldout-margin-propagation-layout-shift|PAT-149]]
