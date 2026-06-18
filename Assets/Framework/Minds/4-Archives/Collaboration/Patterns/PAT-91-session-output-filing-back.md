---
id: PAT-91
title: 会话深度产出主动 filing back 入 Vault
summary: 实质性分析/方案论由 AI 主动提议入库 不再活在聊天历史
status: active
type: pattern
date: 2026-05-25
source: karpathy-llm-wiki
category: obs
aliases:
  - PAT-91-session-output-filing-back
  - session-output-filing-back
tags:
  - pattern
  - obs
  - vault
  - session
  - knowledge-base
  - ai-collab
  - compounding
keywords: [filing back, 复利, 探索, 会话产出, ingest 类型, session output, 持久化, LLM Wiki, compounding]
related:
  - "[[PAT-92-vault-log-append-only|PAT-92]]"
  - "[[PAT-26-ai-concise-reply|PAT-26]]"
  - "[[PAT-47-ai-skill-no-redundant-confirm|PAT-47]]"
  - "[[PAT-89-bookkeeping-cost-near-zero|PAT-89]]"
---

# PAT-91：会话深度产出主动 filing back 入 Vault

## 模式（What）

主会话产出**实质性分析 / 方法论 / 决策建议**时，主动提议入 Vault；用户同意后走 obs skill 落到 `2-Areas/` 或 `3-Resources/`。**不再让"探索成果"只活在聊天历史里**。

## 为什么（Why）

Karpathy LLM Wiki 论断：

> "good answers can be filed back into the wiki as new pages. ... 这样你的探索就和源材料一样在知识库里复利"

Nova 历史问题（本会话亲历）：

- 用户深度提问"提取 Karpathy 方法论对 Nova 的价值" → 主会话产出 5 条建议 + 落地优先级排序 → **不入库就只能下次重做**
- 复杂技术决策的对话推演（如"为什么 ADR-037 解除嵌套限制") → 决策本身入了 ADR，但**推演过程**未沉淀，新人无法复盘
- 用户偏好（如"对 X 不感兴趣，对 Y 优先级最高"）→ 该入 memory，但**理由 / 上下文**应入 obs feedback 长期保存

不入库 = 用户付出注意力换来的洞察被 token 流冲走。

## 怎么用（How to apply）

### 触发条件（满足任一）

- 输出≥3 段、有结构（带标题 / 表格 / 编号清单）的分析
- 比较 / 取舍 / 方法论提炼性质的内容
- 用户明确表示"这个有意思 / 这套思路不错 / 记下来"
- 跨 ≥2 模块或 ≥2 ADR 的综合性结论

### 主动提议格式（一句话）

```text
> 这份分析是否要入 [2-Areas/Patterns/ | 2-Areas/ADR/ | 3-Resources/]？
> 走 /nova-obs {patterns|adr|resource}-to-inbox 起草；不入则只活在本轮会话。
```

非"形式性确认"——而是**给用户决策点**。用户说"不必"也不重复问；用户说"入"则立即起草到 1-Inbox。

### 与 PAT-47 的边界

- **PAT-47 禁止的**：obs 入库 skill 内部"评审完了吗""确认入库吗"等流程性问询
- **本 PAT 鼓励的**：实质性产出后的"是否值得 filing back"决策点——这是**新增工作的入口决策**，不是已决工作的形式确认

### 不该 filing back 的内容

- 调试 / 验证类对话（属于过程，留 git history 即可）
- 仅复述既有 ADR/PAT 的回答（已有沉淀，重复入库 = 噪声）
- 用户明确说"先口头讨论不入库"的内容

## 反模式

- 主会话产出深度分析后**直接结束本轮**，不主动提议入库 → 知识丢失
- 每条回答都问"要不要入库" → 反 PAT-47，污染交互
- 用户没确认就直接调 obs skill 写入 → 越权，违反 PARA + Inbox 评审流程
- 把临时调试日志 filing back → Vault 噪声

## 关联

- 触发场景：用户提"深度阅读" / "方法论提炼" / "梳理一下 X" 时大概率触发
- 入库工具：`/nova-obs {patterns,adr,resource}-to-inbox` 系列
- 边界：[[PAT-47-ai-skill-no-redundant-confirm|PAT-47]]（流程性确认禁止）
- 思想源：Karpathy LLM Wiki "filing back" 复利原则
