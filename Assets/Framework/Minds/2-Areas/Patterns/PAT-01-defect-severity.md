---
id: PAT-01
title: 缺陷严重度 P0-P4 分级
type: pattern
status: active
date: 2026-06-05
summary: Nova 代码审查与问题跟踪统一使用 P0-P4 严重度语言
category: quality
aliases:
  - PAT-01
keywords:
  - PAT-01
  - 缺陷严重度 P0-P4 分级
tags:
  - pattern
  - quality
  - code-review
related:
  - "[[PAT-02-static-review-four-dim|PAT-02]]"
  - "[[PAT-03-runtime-verify-three-step|PAT-03]]"
---

# PAT-01：缺陷严重度 P0-P4 分级

## 适用场景

- 代码审查
- 缺陷排序
- 修复优先级判断

## 分级规则

| 级别 | 含义 | 处置 |
|---|---|---|
| P0 | 崩溃、编译不过、主流程不可用、安全级阻断 | 不修不合入 |
| P1 | 明确逻辑错误、数据错误、契约错误 | 必须修复 |
| P2 | 资源泄露、生命周期失衡、稳定性隐患 | 应优先修复 |
| P3 | 并发、竞态、边界时序风险 | 应登记并尽快处理 |
| P4 | 风格、注释、低风险边界瑕疵 | 可延后 |

## 使用原则

- 严重度越小，优先级越高
- P0 / P1 不和风格问题混在同一优先级里讨论
- 同类问题可以合并描述，但不能模糊严重度

## 为什么这样定

- 没有统一分级时，团队会把“真故障”和“低风险瑕疵”混在一起
- 审查语言统一后，修复排序、合入闸门和风险登记才有可执行性

## 反模式

- 只说“这个问题挺严重”
- 用 P4 风格问题阻塞 P0/P1 修复链路
- 不给问题分级，导致清理顺序混乱

## 关联

- [[PAT-02-static-review-four-dim|PAT-02]]
- [[PAT-03-runtime-verify-three-step|PAT-03]]
