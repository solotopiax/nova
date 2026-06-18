---
id: PAT-59
title: 早期错误调研结论会沉淀为长期工程负担，需定期回炉复核
summary: 错误调研结论会层层固化反向追溯，需周期性回炉看官方源
category: methodology
type: pattern
status: active
date: 2026-05-21
aliases:
  - PAT-59-ai-research-conclusion-staleness
keywords:
  - 早期错误调研结论会沉淀为长期工程负担，需定期回炉复核
  - PAT-59
  - PAT-59-ai-research-conclusion-staleness
tags:
  - pattern
  - research
  - rules
  - evidence
  - review
related:
  - "[[ADR-032-drop-novabehaviour-bridge|ADR-032]]"
---

# PAT-59：早期错误调研结论会沉淀为长期工程负担，需定期回炉复核

## 适用场景（When）

某次早期调研给出的结论被写进 ADR / Pattern / Glossary / 框架代码，并在后续设计、实现与文档中被反复引用。

## 核心做法（What & How）

1. 入库前先取证：在 ADR / 规则文件里明示依据来源，列出官方文档段落、第三方权威或实测代码路径。
2. 为绕过某外部框架“做不到 X”而生造的桥接层，单独列入假设清单。
3. 每次重大版本升级都要重读官方更新日志并复核假设清单；非升级期至少季度复核一次。
4. 发现假设不成立后，起 ADR 翻案并同步删除桥接代码与派生规范。

## 为什么这么做（Why）

- 早期结论一旦被后续文档和代码重复引用，就会形成连锁依赖，推翻成本会成倍放大。
- “无奈”“权宜”“绕开”这类措辞通常意味着根本前提未复核，应立即回炉。
- 历史结论不会自动自证，缺少复核机制时只会沿着旧前提继续扩张。

## 反模式（Anti-patterns）

- 把调研结论直接写成 ADR/Pattern 不附"依据来源"段——未来无法验证
- 用模糊语气写结论（"HybridCLR 不支持……"）而不指官方文档的具体行号
- 发现假设不成立后只删代码不动文档 → 旧结论继续误导后续 AI
- 发现假设不成立后只翻案不删旧 ADR → supersedes 链断裂，新人 AI 拿到旧 ADR 当现行铁律

## 跨项目复用提示

通用方法论，可直接搬到任何“调研结论会进入长期工程资产”的项目。建议落地时同时建立：
- ADR / Pattern frontmatter 统一保留 `evidence:` 字段，链接官方文档
- 周期性例行复核：每个季度至少做一次“evidence 链接是否仍有效”的人工检查
