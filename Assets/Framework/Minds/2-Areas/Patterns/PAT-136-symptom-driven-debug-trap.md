---
id: PAT-136
title: 同源 bug 全链路状态分析，拒绝症状驱动
summary: 同链路反复 bug 先画全链路状态机根除
category: review
type: pattern
status: active
date: 2026-06-03
aliases:
  - PAT-136-symptom-driven-debug-trap
keywords:
  - PAT-136
  - 同源 bug 全链路状态分析，拒绝症状驱动
  - PAT-136-symptom-driven-debug-trap
tags: [pattern, methodology, review, debugging]
related:
  - "[[PAT-135-serializeref-crosscell-deepcopy-trap|PAT-135]]"
---

# PAT-136：同源 bug 全链路状态分析，拒绝症状驱动

## 适用场景（When）

- 同一条代码链路（如「编辑 → 提交 → 广播 → 切换坐标」）短期内反复爆出**不同症状**的 bug。
- 排查中识别到一个**确定性缺陷**，但它「不解释手头正在查的那个症状」。
- 某复现 / 验证手段在当前环境**已被证明不可行**（如无头环境无法注入真实 IMGUI 字符编辑）。

## 核心做法（What & How）

1. **先画全链路状态机，再动手**：对反复出 bug 的链路，系统列出每一步的关键状态，把同族问题一次性枚举，而非逐症状追。本例的链路状态清单：
   - `SerializedObject.Update()` 的同步点在哪、缓存 stale 窗口有多大；
   - `ApplyModifiedProperties()` 回写范围（整树 vs 单 leaf）；
   - 绕过 SO 直改 C# 对象的字段（mask）何时被回写覆盖；
   - `boxedValue` 跨格赋值是否真深拷贝（内存态 vs 存盘态）。
2. **隐患当场根除**：排查中发现的任何确定性缺陷，**当场列入清单并根除**，不得因「与当前症状无关」放过。
3. **无效手段尽早止损**：验证无法在当前环境复现时，立刻转数据层 / 源码逻辑分析，不在不可行手段上空耗。

## 为什么这么做（Why）

- 三个 bug 同源（ConfigWindow 维度编辑链路的 `SerializedObject` 语义：缓存 stale、整树回写、managed ref 共享）。症状驱动导致「修完一个 → 等用户再踩下一个」，本可一次揪出却拖成**一整天三轮返工**。
- 识别到的 mask clobber 隐患，因「不解释字段值复原」被搁置，**直接导致 toggle 复原 bug 二次爆发**——隐患必爆，且爆时已丢失上下文，要从头重排查。
- 在「后台 SendEvent 注入 IMGUI 字符编辑」上反复尝试（环境根本不支持，文本编辑依赖真实 GUIView 键盘焦点），是第一轮最大的时间黑洞。

## 反模式（Anti-patterns）

- **症状驱动收工**：修完手头症状即停，不回扫同链路其他状态点 → 同族 bug 逐个爆、多轮返工。
- **隐患搁置**：识别到确定性缺陷因「跟当前症状无关」放过 → 隐患必爆，且爆发时上下文已失需重排查（本次 toggle 复原即此）。
- **无效手段空耗**：明知某复现手段在当前环境不可行仍反复尝试 → 时间黑洞，挤占真正有效的逻辑分析。

## 跨项目复用提示

- 纯调试方法论，任何技术栈通用。
- 「同源链路画状态机一次根除」尤其适用于状态机 / 缓存同步 / 序列化往返这类「一个语义点错、多处症状」的场景。

## 关联

- 相关 Pattern：[[PAT-135-serializeref-crosscell-deepcopy-trap|PAT-135]]（本次三 bug 之一的技术根因）
- 同源经验已吸收入本文，不再保留外部协作指针。
