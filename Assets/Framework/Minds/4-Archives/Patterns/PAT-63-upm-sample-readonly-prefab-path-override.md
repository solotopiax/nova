---
id: PAT-63
title: UPM Sample 里只读 Prefab 通过 Scene Override 改路径
summary: 已被 PAT-130 修订取代，保留作只读 Prefab 路径 override 历史背景
category: workflow
type: pattern
status: superseded
date: 2026-06-05
aliases:
  - PAT-63-upm-sample-readonly-prefab-path-override
keywords:
  - PAT-63
  - UPM Sample 里只读 Prefab 通过 Scene Override 改路径
  - PAT-63-upm-sample-readonly-prefab-path-override
tags:
  - pattern
  - upm
  - sample
  - prefab
  - publish
related:
  - "[[PAT-61-version-consistency-prefer-manifest|PAT-61]]"
superseded-by:
  - "[[PAT-130-sample-readonly-prefab-path-override-revised|PAT-130]]"
---

# PAT-63：UPM Sample 里只读 Prefab 通过 Scene Override 改路径

## 适用场景

- UPM 包内 Prefab 含有开发期路径字段
- 包导入外部工程后，Prefab 会落到 `Packages/<pkg>/...` 只读区域
- 样例工程需要把这些路径改成 sample 内可用路径

## 核心规则

- 不直接改包内 Prefab 本体
- 通过 sample scene 上的 `PrefabInstance` override 覆盖相关字段
- 必要时在导入阶段再做一次 sample 根路径重写

## 为什么这样定

- 包内 Prefab 在外部工程里是只读资源
- Scene 是样例侧可写入口，且 Unity 原生支持 override 语义
- 这样既保留了开发期 Prefab 的原始语义，也能让导入后的 sample 自洽

## 当前项目里的落地思路

- 发布阶段先准备 sample 内的目标资源
- 从 Prefab 收集需要覆盖的路径字段
- 把这些覆盖写进 sample scene 的 `m_Modifications`
- 导入阶段根据真实 sample 根再做一次路径归一

## 反模式

- 直接改包内 Prefab
- 手工维护一份脆弱的硬编码 override 清单
- 只复制资源，不补 scene override

## 关联

- [[PAT-61-version-consistency-prefer-manifest|PAT-61]]
