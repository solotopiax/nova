---
id: PAT-66
title: 禁止手改 Prefab YAML
summary: Prefab 只走 Unity 正常序列化路径
category: workflow
type: pattern
status: active
date: 2026-06-05
aliases:
  - PAT-66-no-handcraft-prefab
keywords:
  - PAT-66
  - PAT-66-no-handcraft-prefab
  - 禁止手改 Prefab YAML
tags: [pattern, workflow, prefab, editor, serialization]
related:
  - "[[PAT-63-upm-sample-readonly-prefab-path-override|PAT-63]]"
---

# PAT-66：禁止手改 Prefab YAML

## 适用场景

- 创建新的 `.prefab`
- 修改已有 Prefab 的层级、组件、字段、引用
- 调整 UI 相关的 Layout、Mask、ScrollRect、Canvas 等配置

## 核心规则

- 不直接改 `.prefab` 文本
- 必须走 Unity Editor 的真实序列化与保存路径
- 高风险改动后至少做一次 Editor 内可见验证或 Play Mode 验证

## 推荐路径

| 操作 | 推荐方式 |
|---|---|
| 新建或克隆 Prefab | Unity Editor / UnityMCP |
| 进入 Prefab Stage 编辑 | Unity Editor / UnityMCP |
| 修改组件字段、层级、引用 | Unity Editor / UnityMCP |
| 保存与重导验证 | Unity Editor 保存，必要时重新导入 |

## 为什么这样定

- Prefab 字段之间存在大量隐式约束，绕过 Unity 的真实创建与保存路径时，这些约束不会自动暴露
- 手改 YAML 很容易写出“语法正确但运行时错误”的资源
- 本次实际踩坑也证明了：问题往往不是某个字段值错，而是绕过了 Unity 的真实序列化语义

## 反模式

- 直接编辑 `.prefab` YAML
- 手改后只看 diff，不进 Editor 验证
- 把“快一点”当作绕过 Unity 序列化路径的理由
- 用字段值对比替代真实运行验证

## 跨项目复用提示

这条规则适用于所有 Unity 项目。关键不是必须用哪一种工具，而是必须走 Unity 自己的序列化与保存路径。没有 UnityMCP 时，也要通过 Editor 真实操作完成。

## 关联

- [[PAT-63-upm-sample-readonly-prefab-path-override|PAT-63]]
