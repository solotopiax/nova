---
id: PAT-105
title: Demo View API 提示就近显示双色规范
summary: 一按钮一演示接口；中文动作标题配原始 API 名，禁止斜杠并列
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
related:
  - "[[ADR-039-base-demo-view-api-hint-split|ADR-039]]"
  - "[[PAT-151-demo-api-hint-source-prefab-gate|PAT-151]]"
---

# PAT-105：Demo View API 提示就近显示双色规范

## 适用场景（When）

Nova MainDemo 或任何"以 UI 形式向开发者展示框架 API 用法"的示教类 view（即"文档即 UI"模式）。

## 核心做法（What & How）

### 五条铁律

1. **标题区不再放 API**——TitleBar 只放模块名（如 `UI` / `Vibrate` / `Sound`），居中。
2. **API 提示就近挂载到对应交互元素**——粒度是"1 接口 1 提示"。原 `"A / B / C"` 字符串拆开，A 挂调用 A 的按钮、B 挂 B 的、C 挂 C 的。
3. **按钮主文案与 API 提示职责分离**——主文案使用消费端易懂的中文动作名称；副提示保留代码中的原始 API 名称，不得翻译成中文功能说明。
4. **一个按钮只承担一个独立演示目标**——若两个 API 都需要被消费端认识和单独操作，必须拆成两个按钮，不得用 `/`、箭头或其他连接符把多个接口名并列到同一个按钮。用户手势令牌、空值检查、服务端适配等无法独立形成用户操作的调用前置，不计为新的演示目标，副提示只展示本按钮最终要引导消费端调用的公开接口。
5. **颜色双轨制区分元素性质**：
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
| 聚合或快照型按钮 | 选定一个对消费端有明确意义的主接口作为演示目标；多个接口都需要教学时拆成多个按钮 |
| 纯状态展示型（如 DemoConfigView） | 挂主刷新按钮 + SetFieldApiHint 挂主字段 |
| 纯快照型（如 HybridClr 系列无交互） | 不挂任何 hint，避免强行制造主元素 |

## 为什么这么做（Why）

- **就近原则降低学习成本**——用户点哪个按钮就看到那条 API，不需要在屏幕上空中匹配；
- **颜色编码一眼识别交互性**——深蓝=可点、白色=数据展示；
- **粒度对等于交互**——按钮是离散动作，提示也应该是离散的一句签名；
- **代码名承担导航作用**——消费端可直接按副提示搜索公开接口，不需要把中文说明反向猜回类型或方法名；
- **标题区清空让模块名更醒目**——视觉重心回归"我现在在哪个 demo"。

## 反模式（Anti-patterns）

1. **标题区用 `/` 串接多 API**：被截断、不知道对应哪个交互、视觉拥挤。
2. **按钮内用 `/`、`→` 等符号并列多个 API**：无法判断一次点击的主要演示目标，也失去快速定位单一接口的作用。
3. **用中文功能说明替换 API 副提示**：主按钮和副提示信息重复，消费端仍不知道应该搜索哪个接口。
4. **整段 API 字符串挂到主按钮**：粒度太粗，多按钮 view 时用户分不清。
5. **空 view 强行制造主元素挂 hint**：不可读，hint 本来是注释不是主体。
6. **同一 view 同时在标题区和元素旁双重标注**：信息冗余。

## 来源与验证依据

- 设计依据：[[ADR-039-base-demo-view-api-hint-split|ADR-039]] 确立“一接口一提示”，[[PAT-151-demo-api-hint-source-prefab-gate|PAT-151]] 负责源码、Prefab 与可见文本的闭环门禁。
- 2026-09-20 WechatMiniGameDemo 消费端复核：15 个按钮统一为“中文动作标题 + 单一原始 API 名”，移除斜杠组合提示；`RuntimeInfo` 按钮不再同时读取 `CurrentLaunchContext`。
- 自动验证：`WechatMiniGameLocalPackageContractTests` 固定页面标题、API 提示清单及 `RuntimeInfo` 单一职责；相关 Runtime 与 Editor 测试程序集编译 0 error。

## 跨项目复用提示

可搬到任何"以 UI 教学 SDK 用法"的示教项目（教程类、示例类）。游戏内 UI 不需要。
