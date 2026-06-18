---
id: PAT-102
title: 按钮内副提示叠加布局：主文字 stretch+Center 副提示锚点贴底
summary: 主文字双居中+副提示锚点贴底
category: demo
type: pattern
status: active
date: 2026-05-26
aliases:
  - PAT-102-button-overlay-sub-hint-layout
keywords:
  - PAT-102
  - 按钮内副提示叠加布局：主文字 stretch+Center 副提示锚点贴底
  - PAT-102-button-overlay-sub-hint-layout
tags: [pattern, ui, button, layout, inspector]
related: []
---

# PAT-102：按钮内副提示叠加布局——主文字 stretch+Center / 副提示锚点贴底

## 适用场景（When）

按钮需要在主文字之外再挂一行**副提示文本**（如 API 签名、快捷键、单位），且必须满足：

- 主文字垂直**居中**（视觉重心，不可妥协）；
- 副提示**不与主文字重叠**；
- 按钮高度未知 / 多个按钮高度不一致 / 多语言导致主文字行数变化。

## 核心做法（What & How）

### 错误方案（重叠 bug）

主文字与副提示都是 `RectTransform stretch 满父` + `TextAlignmentOptions.Center` → 两者垂直对齐到同一中线 → **重叠**。

### 错误折中方案（被用户否决）

主文字 `Top` + 副提示 `Bottom` → 不重叠但**主文字不再居中**，违反视觉规范。

### 正确方案

**主文字保持 stretch + 双居中，副提示用 RectTransform 锚点把自己拉到按钮底部固定高度**：

```
Button (any height)
├─ Text (主文字, TMP_Text)
│   RectTransform: anchorMin=(0,0) anchorMax=(1,1) offset=(0,0)  // stretch 满父
│   alignment = TextAlignmentOptions.Center                       // 水平+垂直双居中
│
└─ ApiHintText (副提示, TMP_Text)
    RectTransform:
      anchorMin = (0, 0)        // 底边锚点
      anchorMax = (1, 0)
      pivot     = (0.5, 0)
      sizeDelta = (0, 22)       // 固定高度 22px（容纳 18px 字体）
      anchoredPosition = (0, 4) // 距底 4px 留白
    alignment = TextAlignmentOptions.Center
    raycastTarget = false       // 不可拦截父按钮点击
```

要点：

1. **主文字 stretch 满父**——按钮高度变化不需要动主文字，永远占满垂直空间并居中；
2. **副提示锚点贴底**（不是 stretch）——通过 anchor 把自己钉在按钮底边一段固定高度内，与主文字的中线物理上分离；
3. **raycastTarget=false 必须设**——副提示如果拦截 raycast 会让父 Button 点不动（典型 bug）。

## 为什么这么做（Why）

- **主文字不能 Top**：用户硬要求"主文字必须居中"，按钮的视觉重心是主文字；
- **副提示不能 stretch+Center**：会和主文字共用中线导致重叠；
- **副提示用锚点而非 LayoutGroup**：按钮已被父 LayoutGroup 控制高度，按钮内再加 LayoutGroup 会嵌套干扰；锚点方案最轻量，按钮高度怎么变副提示永远贴底。

## 反模式（Anti-patterns）

1. **主文字 + 副提示都 stretch+Center**：直接重叠。
2. **主文字 alignment=Top 折中**：虽然不重叠，但违反“主文字必须居中”的设计约束。
3. **副提示 raycastTarget=true**：父按钮点击事件失效，且该问题在静态 prefab 检视中不明显，必须 Play Mode 验证。
4. **用 LayoutGroup 实现按钮内主+副布局**：嵌套 LayoutGroup 会跟父级抢高度计算，引发 ContentSizeFitter 死循环或一帧延迟。

## 跨项目复用提示

直接可搬。任何 Unity UGUI 项目都受用——只要满足"按钮内主文字居中 + 副提示就近"的设计模式。
