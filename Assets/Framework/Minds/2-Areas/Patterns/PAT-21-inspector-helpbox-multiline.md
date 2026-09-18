---
id: PAT-21
title: Inspector HelpBox 用户视角与多语义分行规则
type: pattern
status: active
date: 2026-06-05
summary: HelpBox 只写用户需要的用途影响与操作且多条信息必须分行
category: inspector
aliases:
  - PAT-21-inspector-helpbox-multiline
  - Inspector HelpBox 用户视角规则
keywords:
  - PAT-21
  - HelpBox 分行
  - HelpBox 用户视角
tags:
  - pattern
  - editor
  - inspector
related:
  - "[[PAT-09-inspector-config-i18n|PAT-09]]"
  - "[[PAT-24-inspector-row-vertical-alignment|PAT-24]]"
---

# PAT-21：Inspector HelpBox 用户视角与多语义分行规则

## 适用场景

- 自定义 Inspector 或 EditorWindow 中为一个字段补充说明
- 一条说明里已经包含 2 条及以上独立语义
- 枚举值、布尔开关、默认行为、例外条件需要并列说明

## 核心规则

- HelpBox 面向功能使用者，只说明配置用途、用户可见影响、必要操作与确实需要用户处理的限制。
- 内部类型、方法、字段名、文件名、请求实现、Handle 生命周期、调用链、回退状态记录等实现细节不得写入 HelpBox；需要保留时写入 Docs、代码注释或诊断日志。
- 技术术语只有在它本身就是用户需要配置、选择或排障的公开概念时才保留。
- 只有 1 条语义时，用单行说明即可。
- 出现 2 条及以上独立语义时，必须拆成多行。
- 多行说明统一用 `(1)`、`(2)`、`(3)` 这种编号风格。
- 每一行只表达一件事，不把“推荐值 + 例外 + 平台差异”重新塞回一行长句。
- 配置项因平台、总开关、子选项或 Play Mode 禁用时，对应 HelpBox 必须进入相同禁用作用域并同步灰显。

## 推荐写法

- 单条说明：一句完整中文。
- 多条说明：每条独立成一个字符串元素，由 HelpBox 自然换行渲染。

## 为什么这样定

- 使用者需要快速判断“这个配置有什么影响、应该怎么选”，不需要理解框架内部实现。
- 暴露内部机制会增加理解成本，并让 HelpBox 随实现调整频繁过期。
- Inspector 横向空间窄，长句换行后语义边界非常不清晰。
- 编号后的多行文本更容易被快速扫描，也更方便按条校对。
- 这能强迫作者先把规则拆清，再写说明。

## 当前落地状态

- `AssetComponentInspector`、`AppComponentInspector`、`EventComponentInspector` 等配置面板已经大量使用多行 HelpBox。
- 旧面板里仍可能存在“单字符串塞多条信息”的残留，因此本条仍是持续治理规则。

## 反模式

- 在 HelpBox 中解释内部类名、方法名、文件后缀、Handle 释放顺序或 Procedure 调用链
- 为证明实现正确，把缓存、重试或回退的内部步骤完整展示给使用者
- 用分号把多条说明串成一整句
- 单条说明也强行编号
- 同一面板内混用 `(1)`、`1.`、`-` 多种编号风格
- 配置项已经灰显，但对应 HelpBox 仍保持高亮

## 关联

- [[PAT-09-inspector-config-i18n|PAT-09]]
- [[PAT-24-inspector-row-vertical-alignment|PAT-24]]
