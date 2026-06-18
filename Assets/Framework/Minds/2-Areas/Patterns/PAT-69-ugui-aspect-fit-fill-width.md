---
id: PAT-69
title: UGUI 等比铺满父宽的零脚本配置法
type: pattern
status: active
summary: ARF+水平 stretch 实现等比满宽零脚本
category: module
date: 2026-05-23
aliases:
  - PAT-69-ugui-aspect-fit-fill-width
keywords:
  - PAT-69
  - PAT-69-ugui-aspect-fit-fill-width
  - UGUI 等比铺满父宽的零脚本配置法
tags:
  - pattern
  - nova
  - ui
  - ugui
  - prefab
---

# PAT-69：UGUI 等比铺满父宽的零脚本配置法

## 适用场景

- 需要让一张图片横向铺满父级宽度，同时保持原图宽高比。
- 父级宽度运行时动态变化，且希望零脚本实现。

## 核心做法

目标节点（如 Logo）按下表配置：

| 组件 | 字段 | 值 |
|---|---|---|
| RectTransform | AnchorMin / AnchorMax | `(0, 0.5)` / `(1, 0.5)` 水平 stretch |
| RectTransform | sizeDelta | **`(0, 0)`**（宽=父宽，高由 ARF 算） |
| Image | preserveAspect | `1`（保险） |
| AspectRatioFitter（新增） | AspectMode | `WidthControlsHeight`（枚举值 **1**） |
| AspectRatioFitter | AspectRatio | 原图 `width / height`，如 `2018/576 ≈ 3.5034723` |
| ContentSizeFitter | — | **必须删除**（否则覆盖 anchor stretch） |

关键点：
- `AspectMode=1` 表示宽度作为输入，由 ARF 自动驱动高度。
- `sizeDelta.x` 必须保持 0。
- `preserveAspect` 作为双重保险即可。

## 反模式

- ❌ 用 `ContentSizeFitter` 横向 PreferredSize + Image：宽度由 sprite 原图驱动，不会跟父级走
- ❌ Anchor 居中 + 固定 sizeDelta：屏宽变化时图片不缩放
- ❌ AspectMode 选错枚举（如 `2 = HeightControlsWidth`）：方向反了，宽度会被高度反推
- ❌ 同时挂 ContentSizeFitter + AspectRatioFitter：互相覆盖，行为不可预期
- ❌ 在 prefab stage 看到父级 size=100×100 或 root scale=0 就以为配置错了：那是 prefab 编辑环境特性，必须进 Play Mode 看 Canvas 实算尺寸
