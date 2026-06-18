---
id: PAT-39
title: EditorUtil.Draw 纪律强化规则
type: pattern
status: active
date: 2026-06-05
summary: 缺 Draw 接口先补接口再改 UI
category: editor
aliases:
  - PAT-39-editor-draw-discipline-enforcement
keywords:
  - PAT-39
  - Draw 纪律
tags:
  - pattern
  - editor
  - discipline
related:
  - "[[PAT-35-editor-draw-only|PAT-35]]"
  - "[[PAT-46-iteration-grep-self-check|PAT-46]]"
---

# PAT-39：EditorUtil.Draw 纪律强化规则

## 适用场景

- 修改已有 Inspector 或 Window 的布局
- 发现 `EditorUtil.Draw` 现有接口不够表达目标 UI
- 处理历史遗留的裸 IMGUI 调用

## 核心规则

### 1. 缺接口时，优先扩 Draw

- 不因为缺少某个 helper，就把新代码写回原生 IMGUI。
- Draw 缺什么，就在 Draw 里补什么。

### 2. 新代码不再引入新的裸调用

- 历史残留可以分批治理。
- 但本次触达的区域，不应再新增新的业务层裸调用。

### 3. 局部改造时顺手收口同一片区

- 如果正在修改一个 Inspector 的布局，优先把同文件内相邻的裸调用一起整理掉。
- 不要求“一次性清零全库”，但要求“不要越改越分裂”。

## 当前代码事实

- 当前 Editor 代码里仍能找到部分旧式写法与历史缩进方式。
- 这说明 PAT-35 已成为主方向，但仍需要 PAT-39 这种执行层约束来防止回潮。

## 为什么这样定

- 规范失效通常不是因为原则错，而是因为“临时绕过”积少成多。
- Draw 纪律如果没有执行层约束，很快会退化成“有就用，没有算了”。
- Nova 的 Editor UI 一旦允许多套风格并存，后续审计成本会指数上升。

## 反模式

- 口头认同 `EditorUtil.Draw`，改代码时仍直接回退到原生 API
- 发现缺接口只写 TODO，不当场补 Draw
- 在同一个 Inspector 中同时引入新 Draw 写法和新的裸调用

## 关联

- [[PAT-35-editor-draw-only|PAT-35]]
- [[PAT-46-iteration-grep-self-check|PAT-46]]
