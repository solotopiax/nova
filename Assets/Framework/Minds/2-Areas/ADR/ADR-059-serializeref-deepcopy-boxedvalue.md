---
id: ADR-059
title: SerializeReference 跨格深拷贝改用 boxedValue
status: accepted
summary: 跨格深拷贝改用 JsonUtility round-trip
category: editor
date: 2026-06-02
aliases:
  - ADR-059-serializeref-deepcopy-boxedvalue
keywords:
  - ADR-059
  - SerializeReference 跨格深拷贝改用 boxedValue
  - ADR-059-serializeref-deepcopy-boxedvalue
tags:
  - nova
  - config
  - editor
  - dimension-projector
related:
  - "[[PAT-135-serializeref-crosscell-deepcopy-trap|PAT-135]]"
---

# ADR-059：SerializeReference 跨格深拷贝改用 boxedValue

## 背景

跨格深拷贝 `[SerializeReference]` 时，Unity 6000 的 `boxedValue` 在编辑期会保持同一对象引用，改一格会污染同组其他格。

## 决策

- 跨格深拷贝统一改用 `JsonUtility` round-trip。
- `DimensionProjector.FillGroupSerializedRef` 只保留真正会分裂实例的做法。

## 影响

- 编辑器内的多态配置可以稳定分裂成独立实例。
- `CopyFromSerializedProperty` 和 `boxedValue` 都不再作为这条链路的默认方案。

## 关联

- 相关 Pattern：`PAT-135`
