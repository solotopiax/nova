---
id: PAT-79
title: Demo View 竖屏布局铁律（768×1666 + match-by-width）
summary: 竖屏768x1666 三段式 Scaler运行时注入
category: docs
type: pattern
status: active
date: 2026-05-24
aliases:
  - PAT-79-demo-view-portrait-layout
keywords:
  - PAT-79
  - Demo View 竖屏布局铁律（768×1666 + match-by-width）
  - PAT-79-demo-view-portrait-layout
tags:
  - pattern
  - sample
  - demo
  - ui
  - layout
  - portrait
  - canvas
related:
  - "[[PAT-77-base-demo-view-three-zone-template|PAT-77]]"
  - "[[PAT-80-demo-view-pure-color-style|PAT-80]]"
  - "[[ADR-033-maindemo-isolated-topology|ADR-033]]"
---

# PAT-79：Demo View 竖屏布局铁律

## 适用场景（When）

- 新建或修改任何 BaseDemoView 派生的 DemoXxxView Prefab。
- 修改 BaseDemoView.prefab 基础骨架。

## 核心做法（What & How）

### 画布参数（铁律）

| 参数 | 值 | 说明 |
|---|---|---|
| 设计分辨率 | 768 × 1666 | 竖屏标准分辨率 |
| ScreenMatchMode | MatchWidthOrHeight | 按宽度适配 |
| m_ScreenWidthHeightMatchValue | 0 | 全 width-match，高度不参与缩放 |
| CanvasScaler 写入方式 | UIComponent.ApplyInstanceRootCanvasScaler 运行时注入 | **Prefab 不写死画布尺寸**，防止不同分辨率下 Variant 参数漂移 |

### 三段式高度（铁律）

| 区域 | 高度 | Anchor 方式 |
|---|---|---|
| TitleBar | 120px 固定 | Top anchor（(0,1)→(1,1)），pivot=(0.5,1)，anchoredPosition=(0,0)，sizeDelta=(0,120) |
| InteractionArea | **比例拉伸（父高×0.6 - 120px）** | anchorMin=(0,0.4) anchorMax=(1,1)，pivot=(0.5,1)，anchoredPosition=(0,-120)，sizeDelta=(0,-120)（顶偏移 120px 让给 TitleBar） |
| FeedbackArea | **比例拉伸（父高×0.4）** | anchorMin=(0,0) anchorMax=(1,0.4)，pivot=(0.5,0)，anchoredPosition=(0,0)，sizeDelta=(0,0) |

> **三区无重叠说明**：FeedbackArea 占底部 0~0.4，InteractionArea 从 0.4 向上到 1.0 再减去顶部 120px TitleBar 偏移，TitleBar 固定顶部 120px。在 1366px 参考高度下实测：TitleBar=120px，InteractionArea≈787px，FeedbackArea≈546px，三区 y 区间不交叠。

### RootBackground（铁律）

- 在 BaseDemoView.prefab **index 0**（根节点第一子节点）挂 Image 组件
- 颜色：RGBA (0.05, 0.05, 0.08, 1)（深灰近黑）
- 35 个 Variant 自动继承，无需单独设置

### 按钮 / 文字样式约束（配合 [[PAT-80-demo-view-pure-color-style|PAT-80]]）

| 控件 | 样式 |
|---|---|
| 按钮背景 | 白色 Image（无 Sprite，纯色） |
| 按钮文字 | TMP_Text，黑色，fontSize=28，不换行，居中对齐 |
| 标签文字（反馈区 / 信息卡） | TMP_Text，白色，fontSize=24 |

## 为什么这么做（Why）

- 768 宽度配合 width-match，能让字体在主流竖屏设备上保持稳定。
- CanvasScaler 运行时注入，避免 Prefab 序列化参数和框架策略冲突。
- RootBackground 统一为深灰，减少 Variant 错配。
- TitleBar 120px 保证足够触摸面积。
- FeedbackArea 用比例而不是固定像素，避免不同分辨率下互相挤压。

## 反模式（Anti-patterns）

- **直接修改 Variant 的 CanvasScaler**：CanvasScaler 由框架注入，Prefab 不应序列化此字段
- **TitleBar < 120px**：竖屏触摸目标不足，用户误触概率上升
- **FeedbackArea 固定像素而非 anchor 比例**：固定像素在不同分辨率下易造成三区重叠；应用 anchorMax.y=0.4 比例拉伸
- **设计分辨率用横屏（如 1920×1080）**：竖屏 Demo 在横屏分辨率下 UI 元素过小，height-match 下字体失控缩放

## 关联

- 上游模板：[[PAT-77-base-demo-view-three-zone-template|PAT-77]]（三段式骨架总描述，本 PAT 专注竖屏参数细节）
- 样式规范：[[PAT-80-demo-view-pure-color-style|PAT-80]]（纯色块 + TMP 强制）
- 演示拓扑：[[ADR-033-maindemo-isolated-topology|ADR-033]]（sample 独立闭包原则）
- 相关规则：Demo View 竖屏布局约束
