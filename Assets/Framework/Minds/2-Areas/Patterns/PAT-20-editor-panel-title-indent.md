---
id: PAT-20
title: Editor 配置详情页标题与缩进规则
type: pattern
status: active
date: 2026-06-05
summary: 复杂 Editor 配置面板应有明确标题，并给标题下条目统一缩进
category: inspector
aliases:
  - PAT-20-editor-panel-title-indent
keywords:
  - PAT-20
  - Editor 配置详情页标题与缩进规则
  - PAT-20-editor-panel-title-indent
tags:
  - pattern
  - editor
  - ui-style
related:
  - "[[PAT-09-inspector-config-i18n|PAT-09]]"
  - "[[PAT-21-inspector-helpbox-multiline|PAT-21]]"
---

# PAT-20：Editor 配置详情页标题与缩进规则

## 适用场景

- `ConfigWindow` 一类右侧配置详情页
- Inspector 中存在明显分区的复杂配置块

## 核心规则

- 面板级配置区应有可读标题，不要直接把字段平铺到底
- 标题下的条目应有统一缩进，形成“标题 -> 内容”的视觉层级
- 只有在确实存在子分区时，才继续增加下一层缩进

## 为什么这样定

- 复杂配置面板如果没有标题和缩进，阅读成本会迅速上升
- 统一层级语言后，跨面板切换时更容易定位当前正在配置的内容

## 反模式

- 复杂面板没有标题
- 每个区域各自定义一套缩进数值
- 为了“看起来丰富”滥用多层嵌套

## 关联

- [[PAT-09-inspector-config-i18n|PAT-09]]
- [[PAT-21-inspector-helpbox-multiline|PAT-21]]
