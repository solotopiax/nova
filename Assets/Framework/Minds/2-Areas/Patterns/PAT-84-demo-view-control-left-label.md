---
id: PAT-84
title: Demo View 控件左侧 Label 模式（酌情 + 140px 固定列）
summary: 酌情加左 Label 140px+Row HLG 撑剩余
category: docs
type: pattern
status: active
date: 2026-05-24
aliases:
  - PAT-84-demo-view-control-left-label
keywords:
  - PAT-84
  - Demo View 控件左侧 Label 模式（酌情 + 140px 固定列）
  - PAT-84-demo-view-control-left-label
tags:
  - pattern
  - sample
  - demo
  - ui
  - layout
  - label
related:
  - "[[PAT-77-base-demo-view-three-zone-template|PAT-77]]"
  - "[[PAT-79-demo-view-portrait-layout|PAT-79]]"
  - "[[PAT-80-demo-view-pure-color-style|PAT-80]]"
---

# PAT-84：Demo View 控件左侧 Label 模式

## 适用场景（When）

- DemoXxxView 中的输入控件单看本身不能直接表达“输入什么”时。
- 同行出现多个同类控件，或控件承载业务概念时。

> 反向：按钮（语义已显式写在按钮 Text 上）、单独一个 Toggle（功能不言自明）通常**不需要**左侧 Label。

## 核心做法（What & How）

### 1. 酌情原则

**加 Label 的判断**：
- 控件不能"看一眼就知道输入什么" → 加
- 同行/同区出现 ≥ 2 个同类控件 → 加（区分歧义）
- 强制术语必须出现在 Label 中（如统一写法"Asset 地址"）→ 加

**不加 Label 的判断**：
- 按钮（按钮文字本身就是 Label）
- placeholder 已能表意且单控件独占一行
- Toggle/Checkbox 已自带 Label 字段

### 2. 行结构（Row）规范

| 节点 | 角色 | 关键设置 |
|---|---|---|
| `xxxRow` | 容器 GameObject | RectTransform anchor (0,1)→(1,1), pivot (0.5, 1), height = 72 |
| - HorizontalLayoutGroup | 布局组件 | spacing = 8, MiddleLeft, childControlWidth + childControlHeight = true |
| - `xxxLabel` | 左侧标题 | TMP_Text 白(1,1,1,1), fontSize=24, MidlineRight, enableWordWrapping=false |
| - LayoutElement (Label) | 列宽 | minWidth=140, preferredWidth=140, **flexibleWidth=0** |
| - `xxxControl` (原控件) | 受控控件 | 原 InputField/Dropdown 等 |
| - LayoutElement (Control) | 列宽 | minWidth=0, preferredWidth=0, **flexibleWidth=1**, minHeight/preferredHeight=72 |

> **铁律**：Label flexibleWidth=0；Control flexibleWidth=1 → Label 固定 140，Control 撑满剩余。

### 3. Label 列宽统一为 140px

- 模块内所有 Demo View 同款（避免逐 View 自调出参差不齐）
- 768px 竖屏宽度下：Label 140 + spacing 8 + Control 620 = 768，**字号 24 不会截断常用业务术语**
- 若 Label 文字超出 140 → 检查文案是否过长（如"Asset 地址"4 字 vs "请求 URL"4 字皆稳妥），不得通过缩字号或加省略号"凑短"

### 4. Label 文案规范

- 使用强制术语（命中 `0-Index-Terms.md` 的必须按表对齐：例 ✅"Asset 地址" / ❌"资产地址 / 资源地址 / 资源路径"）
- 中文为主；英文专有名词原样保留（如"JSON" / "AES" / "MD5" / "URL"）
- 4-6 字最佳，避免 ≥ 8 字（140 列宽放不下）

### 5. 字段绑定保护（铁律）

批量改 Prefab 时使用 `PrefabUtility.LoadPrefabContents` + `SaveAsPrefabAsset`，把原控件 `Transform.SetParent(row, false)` 移入新建 Row 内即可——**Unity 序列化按 instanceID 跟踪引用**，移动节点不破坏 View 脚本上 `[SerializeField]` 字段绑定。

> 真实验证：12 个 Variant、17 个控件移入 Row 后字段绑定 17/17 全部存活。

## 为什么这么做（Why）

1. 酌情加 Label，避免通用 UI 全加导致行高翻倍。
2. 140px 固定列保证模块内视觉一致。
3. Control 用 flex 1 撑满剩余空间。
4. Row 包装能让 HorizontalLayoutGroup 只负责这一行。

## 反模式（Anti-patterns）

```text
❌ Label flexibleWidth=1：会和 Control 抢宽度，列宽不稳定
❌ 不同 View 用不同 Label 列宽（120/140/160 混用）：模块内视觉碎片
❌ 把 Label 写成 button-text 风格（黑色）：违反 PAT-80 颜色二分（白字=只读 / 黑字=可交互）
❌ Label 文字超 140 选择缩字号：把统一字号原则破坏
❌ 直接编辑 Prefab YAML 加 Row：违反“Prefab / Scene / Asset 不手改原始 YAML”铁律，Unity 工具链才是唯一通道
```

## 跨项目复用提示

- 列宽 140px 是基于 768 竖屏 + 字号 24 推导的；其他分辨率/字号需要重新算
- "酌情加"的判断标准**强烈推荐保留**：通用 UI 模式都遵循这个原则会更精炼
- Row 结构（HLG + Label LE + Control LE）是纯 Unity UI 通用方案，零项目依赖
