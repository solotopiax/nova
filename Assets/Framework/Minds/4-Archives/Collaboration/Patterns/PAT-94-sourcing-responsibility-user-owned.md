---
id: PAT-94
title: Sourcing 责任归用户 AI 不主动 hunt 文献
summary: AI 不主动找文献 用户给材料才 ingest
status: active
category: ai-collab
type: pattern
date: 2026-05-25
source: karpathy-llm-wiki
aliases:
  - PAT-94-sourcing-responsibility-user-owned
  - sourcing-responsibility-user-owned
tags:
  - pattern
  - ai-collab
  - vault
  - sourcing
  - scope
  - methodology
keywords: [sourcing, hunt, 主动搜文献, WebFetch, 自我膨胀, 用户主导, LLM 角色, Karpathy LLM Wiki, exploration]
related:
  - "[[RES-01-karpathy-llm-wiki|RES-01]]"
  - "[[PAT-15-agent-scope-discipline|PAT-15]]"
  - "[[PAT-91-session-output-filing-back|PAT-91]]"
  - "[[PAT-89-bookkeeping-cost-near-zero|PAT-89]]"
---

# PAT-94：Sourcing 责任归用户 AI 不主动 hunt 文献

## 模式（What）

LLM 在 Vault 协作中**只承担 grunt work（ingest / lint / cross-reference / refactor）**；**sourcing（找新文献 / 选材 / 设方向）由用户主导**。AI **不应**自发 WebFetch / WebSearch / 跨域翻找未指定文献。

> "You're in charge of sourcing, exploration, and asking the right questions. The LLM does all the grunt work." — Karpathy LLM Wiki

## 为什么（Why）

Karpathy 范式核心分工：

| 角色 | 职责 |
|---|---|
| 用户 | sourcing（给文献/材料）、exploration（提关键问题）、curation（决定入不入 wiki） |
| LLM | ingest（摘录联动）、lint（5 维健康检查）、refactor（按 schema 重组）、bookkeeping |

Nova 现状的潜在风险（Bedrock 主体常见漂移）：

- 用户问"X 是什么"，AI 倾向于"我去搜一下"——但 X 在 Vault 内已有 GLO/PAT，搜外部反而引入信噪比低的二手内容
- AI 自发 WebFetch 引入未经用户审定的"外部观点"，混入 PAT 提炼成假独立结论
- 调用工具消耗 token / 网络成本；用户没要、不审，工具结果价值 < 噪声

**AI 主动找资料 = 角色越位**——把 sourcing 责任偷渡给 LLM，违反 Karpathy 核心论断。

## 怎么用（How to apply）

### 允许的 AI 主动行为

| 场景 | 允许工具 |
|---|---|
| 用户给链接/材料后 ingest | WebFetch（仅用户提供的 URL）|
| Vault 内查询 | obsidian_search_notes / grep / Read |
| 代码/项目内查询 | Grep / Read / UnityMCP |
| Karpathy 论断已有 RES 留底，再次引用 | 优先读 RES，原文 URL 仅做核对（可选 WebFetch）|

### 禁止的 AI 主动行为

| 反模式 | 处置 |
|---|---|
| 用户问问题 → AI 自发 WebSearch 找新源 | 先在 Vault 找；找不到则**反问用户是否需要找外部文献**，不自行启动 |
| 起草 PAT 时凭"我大概记得 Karpathy 怎么说" | 必须读 RES 留底，禁凭印象（违反 Nova「实事求是铁律」）|
| AI 提议"我去翻翻 Unity 官方文档"扩写 PAT | 让用户给具体页面或 Unity Manual MCP 的明确入口 |
| `/nova-obs *-to-inbox` 内自动追加"建议读这些文章" | 提议入 1-Inbox 的草稿不夹带 AI 自挖的外部链接 |

### 用户不在场时的 fallback

| 场景 | 行为 |
|---|---|
| 子 agent 派活时无法访问用户 | scope 内能找则找，找不到结论标"未确认"，不外搜 |
| 用户明确说"自己看着办" | 仅在 Vault + 项目内自决，仍不外搜 |
| 用户明确说"去查一下 X 的最新版本" | 这是显式 sourcing 授权，可执行 |

## 与 PAT-15 / PAT-91 的边界

| 维度 | PAT |
|---|---|
| **agent 派活别越位改文件** | [[PAT-15-agent-scope-discipline|PAT-15]] |
| **会话深度产出 filing back** | [[PAT-91-session-output-filing-back|PAT-91]] |
| **AI 别自己找新源**（本 PAT） | sourcing 归用户 |

三者都是"AI 主动行为的边界"，分别管：**改不该改的文件 / 不归档该归档的产出 / 找不该找的外部源**。

## 反模式

- 用户问"X 怎么实现"，AI 不查 Vault 不读代码先 WebSearch
- AI 在草稿 frontmatter `keywords:` 里自发塞自己 search 出来的外部链接
- 子 agent 在 prompt 里被允许"自由查阅文献"——派活 prompt 必须明确"严禁外部 WebFetch / WebSearch，除非清单内 URL"
- 用户问"Vault 还缺什么"AI 直接外搜补缺——应当先跑 obs-health 7 数据空缺检测看 Vault 内信号

## 关联

- 思想源：[[RES-01-karpathy-llm-wiki|RES-01]]
- 边界 PAT：[[PAT-15-agent-scope-discipline|PAT-15]] / [[PAT-91-session-output-filing-back|PAT-91]]
- 顶层哲学：[[PAT-89-bookkeeping-cost-near-zero|PAT-89]] AI 干活但不超工
