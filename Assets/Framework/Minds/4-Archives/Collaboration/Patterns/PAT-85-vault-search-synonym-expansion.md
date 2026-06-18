---
id: PAT-85
title: Vault 检索同义词扩张铁律（命不中复盘四步法）
summary: grep 同义词+占位双链顺藤+命中必读全文+索引兜底
category: workflow
type: pattern
status: active
date: 2026-05-24
aliases:
  - PAT-85-vault-search-synonym-expansion
tags:
  - pattern
  - workflow
  - ai-collab
  - obs
  - search
related:
  - "[[PAT-17-obs-memory-split|PAT-17]]"
  - feedback_obs_rules_pre_action_lookup
---

# PAT-85：Vault 检索同义词扩张铁律

## 适用场景（When）

- 任何 agent / 主会话在「动手前」做 Vault 前置查询
- code-reviewer / runtime-coder / editor-coder 用 grep + obs MCP 检索是否已有规则
- 用户问"有没有 X 规则"，AI 检索后准备答"未找到"

## 核心做法（What & How）

### 检索四步法（缺一步即视为未检索）

#### 1. 关键字必同义扩张

不能只用用户原话当 grep 关键字。原话往往是抽象描述（"颜色区分"），Vault 内的规则可能用具体落地词（"白底黑字"/"黑色"/"白色"/"黑字"/"色块"）。

**模板**：
```bash
# 用户问："有没有按钮颜色规则？"
# ❌ 错误检索（关键字过窄）
grep -rln "按钮颜色" Assets/Framework/Minds/

# ✅ 正确检索（同义扩张）
grep -rln -E "按钮|颜色|白底|黑字|色块|白色|黑色|tmp|color" Assets/Framework/Minds/
```

**扩张维度**：
- 抽象 → 具体（"颜色区分" → "白底黑字"）
- 中文 → 英文（"对齐" → "alignment" / "Width"）
- 概念 → 字段名（"间距" → "spacing" / "padding"）
- 术语别名（命中 `0-Index-Terms.md` 强制术语表）

#### 2. 已有规则可能就是用户问的"基线"——不要预设过滤

用户问"区分 A 和 B"时，可能 Vault 中已有"A 默认黑、B 默认白"——这条规则就是用户问的"区分"，**不是无差别基线**。

**反模式**：grep 命中 PAT-80 的"按钮白底黑字"，但脑内自语"这是基础规则不是颜色区分规则"，直接跳过。

**正解**：grep 命中 = 必读全文 = 不允许预设过滤；只有读完确认与问题无关才能跳过。

#### 3. 占位双链失效要顺藤摸瓜到 0-Index

memory 中的 `[[PAT-XX-slug]]` 占位双链可能：
- 编号已变（`[[PAT-79]]` 现已是 `PAT-77`）
- 链接还没回填（DRAFT 已入库但反向链没换）

grep 用 memory 中的占位名找不到时，**必须**:
```bash
# 兜底通道：通读 0-Index/0-Index-PAT.md 的 summary 列表
Read Assets/Framework/Minds/0-Index/0-Index-PAT.md
# 或者：
grep -rln "<相关关键字>" Assets/Framework/Minds/2-Areas/Patterns/
```

#### 4. 命中后必读全文

grep 命中文件 ≠ 看到内容。必须 `Read <file>` 把命中文件读完，**禁用脑内的"我大概知道这文件讲啥"过滤掉**——记忆与现实经常不一致。

### 检索失败止损

四步走完仍未命中 → 才能答"未找到"，且必须**显式列出已检索的关键字范围 + 已读的兜底文件路径**，让用户能验证你确实查过。

## 为什么这么做（Why）

真实失败案例（2026-05-24 对话）：
- 用户："有没有按钮颜色和文字颜色区分的规则？"
- AI："实事求是答复——我把 memory 和 obs 全检了一遍，没有找到"按钮颜色和文字做颜色区分"的规则。"
- 用户："可是 obs 中明明是有的呀..."（PAT-79/80 实际已覆盖）

**四个失败原因**（用户点名复盘）：
1. **grep 关键字过窄**：搜"颜色"字面，没扩张到"白底/黑字/色块"等落地词
2. **预设过滤**：把已有规则当"无差别基线"忽略，没意识到用户问的"区分"就是已有基线
3. **占位双链失效未顺藤**：memory 中的过期占位双链点不到 0-Index-PAT，没用 0-Index 兜底
4. **grep 命中却没读全文**：脑子用"我记得这文件大概讲 X"过滤掉了已命中内容

**根本逻辑**：检索失败的代价不是"漏查一条"，而是**让 AI 在用户面前说瞎话**——后续用户对一切"未找到"答复都要二次质疑，AI 信用清零。

## 反模式（Anti-patterns）

```bash
# ❌ 反模式 1：原话当关键字直接 grep
grep -rln "颜色区分" Assets/Framework/Minds/   # 命中 0 条 → 答"未找到" → 实际有

# ❌ 反模式 2：grep 命中后用记忆过滤
# 命中 PAT-80 → 脑内"这是颜色规则不是区分规则" → 跳过不读

# ❌ 反模式 3：占位双链找不到就放弃
# memory 写 [[PAT-79]] → grep "PAT-79" 命中 0 条 → 直接答"链接失效"
# 正解：去 0-Index-PAT.md 按 summary 找，可能是编号变了

# ❌ 反模式 4：仅检 obs 不检 memory（或反向）
# 双通道铁律 = 两边都要查
```

## 跨项目复用提示

- 知识库类工具（Notion / Confluence / Obsidian / Logseq）都适用
- "命中必读全文"是 LLM agent 通病的解药：LLM 倾向于用"我记得"代替"我读了"
- 同义词扩张不限于检索，写 Vault 时也要在 frontmatter `aliases:` 写入同义词，让别人查得到

## 来源（Origin）

- 会话日期：2026-05-24
- 关键对话节选：
  > 用户："你 review 一遍我之前给你说的规则，不是让你把按钮颜色和文字做颜色上的区分吗？你为什么没有按照我的要求执行？"
  > 用户："可是 obs 中明明是有的呀，是什么原因你没有检索到？分析下原因，避免下次再犯类似命不中的问题。"
  > AI 复盘：4 个失败原因，沉淀为本 Pattern

## 关联

- 双通道铁律：[[PAT-17-obs-memory-split|PAT-17]]（memory + obs 分工）
- 改前查铁律：[[feedback_obs_rules_pre_action_lookup]]（动手三问 + grep 自检）
- 强制术语对齐：`Assets/Framework/Minds/0-Index/0-Index-Terms.md`
