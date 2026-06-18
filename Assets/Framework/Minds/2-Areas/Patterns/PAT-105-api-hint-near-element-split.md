---
id: PAT-105
title: Demo View API 提示就近显示双色规范
summary: 一接口一提示就近挂；按钮深蓝、字段白色，标题区清空
category: demo
type: pattern
status: active
date: 2026-05-26
aliases:
  - PAT-105-api-hint-near-element-split
keywords:
  - PAT-105
  - Demo View API 提示就近显示双色规范
  - PAT-105-api-hint-near-element-split
tags: [pattern, demo, ui, api-hint, doc-as-ui]
related: []
---

# PAT-105：Demo View API 提示就近显示双色规范

## 适用场景（When）

Nova MainDemo 或任何"以 UI 形式向开发者展示框架 API 用法"的示教类 view（即"文档即 UI"模式）。

## 核心做法（What & How）

### 三条铁律

1. **标题区不再放 API**——TitleBar 只放模块名（如 `UI` / `Vibrate` / `Sound`），居中。
2. **API 提示就近挂载到对应交互元素**——粒度是"1 接口 1 提示"。原 `"A / B / C"` 字符串拆开，A 挂调用 A 的按钮、B 挂 B 的、C 挂 C 的。
3. **颜色双轨制区分元素性质**：
   - **按钮内副提示**：深蓝 `#1A3A8C`，FontSize 18，水平居中，贴按钮底部（参考 [[PAT-102-button-overlay-sub-hint-layout|PAT-102]]）；
   - **字段下方副提示**：白色 `#FFFFFF`，FontSize 18，左对齐，挂在主显示 TMP 的同级或子节点。

### BaseDemoView 工具方法

```csharp
SetButtonApiHint(Button button, string hint)   // 按钮内查找名 "ApiHintText" 子 TMP 赋值
SetFieldApiHint(TMP_Text owner, string hint)   // 字段下查找名 "ApiHintText" 子 TMP 赋值
```

工具方法**不在运行时创建节点**，子节点必须在 prefab 编辑期已添加；运行时找不到就静默跳过。

### 拆解粒度判定（按 view 形态）

| view 形态 | 拆解策略 |
|---|---|
| 多按钮交互型（如 DemoEventView 的 Subscribe/Fire/Unsubscribe） | 1 接口 1 按钮，逐一挂 |
| 单按钮聚合型（如 DemoFsmView 一个 Snapshot 按钮触发多 API） | 多 API 合并挂同一按钮 |
| 纯状态展示型（如 DemoConfigView） | 挂主刷新按钮 + SetFieldApiHint 挂主字段 |
| 纯快照型（如 HybridClr 系列无交互） | 不挂任何 hint，避免强行制造主元素 |

## 为什么这么做（Why）

- **就近原则降低学习成本**——用户点哪个按钮就看到那条 API，不需要在屏幕上空中匹配；
- **颜色编码一眼识别交互性**——深蓝=可点、白色=数据展示；
- **粒度对等于交互**——按钮是离散动作，提示也应该是离散的一句签名；
- **标题区清空让模块名更醒目**——视觉重心回归"我现在在哪个 demo"。

## 反模式（Anti-patterns）

1. **标题区用 `/` 串接多 API**：被截断、不知道对应哪个交互、视觉拥挤。
2. **整段 API 字符串挂到主按钮**：粒度太粗，多按钮 view 时用户分不清。
3. **空 view 强行制造主元素挂 hint**：不可读，hint 本来是注释不是主体。
4. **同一 view 同时在标题区和元素旁双重标注**：信息冗余。

## 跨项目复用提示

可搬到任何"以 UI 教学 SDK 用法"的示教项目（教程类、示例类）。游戏内 UI 不需要。
