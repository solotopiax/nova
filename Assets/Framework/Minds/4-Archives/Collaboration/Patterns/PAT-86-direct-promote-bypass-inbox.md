---
id: PAT-86
title: obs 直接入库语义铁律（"直接入库"=跳过 1-Inbox）
summary: '直接入库=Write 直入 2-Areas 跳 Inbox'
category: obs
type: pattern
status: active
date: 2026-05-24
aliases:
  - PAT-86-direct-promote-bypass-inbox
tags:
  - pattern
  - obs
  - workflow
  - ai-collab
related:
  - "[[PAT-17-obs-memory-split|PAT-17]]"
---

# PAT-86：obs 直接入库语义铁律

## 适用场景（When）

- 用户在本轮消息中**明确**说出"直接入库 / 跳过 inbox / 一步到 2-Areas"
- 起草类 obs skill（`nova-obs {cur,adr,patterns,glossary,resource,memory}-to-inbox`）触发后

## 核心做法（What & How）

### 语义铁律

**"直接入库"的唯一正解**：草稿**不落** `1-Inbox/`，直接 Write 到 `2-Areas/{ADR,Patterns,Glossary}/` 或 `3-Resources/`。

**不是**：先 Write 到 `1-Inbox/` → 再 mv 到 `2-Areas/`。这是"经 Inbox 中转后入库"，违背"直接"语义。

### 操作步骤

1. **取下一空闲编号**（直接读 2-Areas 目录）：
   ```bash
   ls Assets/Framework/Minds/2-Areas/Patterns/ | grep -oE '^PAT-[0-9]{2,}' | sed 's/PAT-//' | sort -n | tail -1
   ```
2. **Write 直入** `2-Areas/{ADR,Patterns,Glossary}/PAT-XX-<slug>.md`
   - frontmatter：`id: PAT-XX`（不写 `PAT-DRAFT`）/ `status: active`（PAT/RES）或 `accepted`（ADR）
   - aliases 首项：`PAT-XX-<slug>`（不带 `-DRAFT-YYYY-MM-DD-` 前缀）
   - 一级标题：`# PAT-XX：<中文标题>`
3. **触发索引重建**：`python3 .claude/skills/nova-obs/scripts/rebuild_index.py --check-bloat`
4. **关联反向链**：相关条目里加 `[[PAT-XX-...|PAT-XX]]`

### 默认路径仍走 Inbox

用户**未明确说**"直接入库"时，一律走默认路径：
- 起草 skill → `1-Inbox/<TYPE>-DRAFT-YYYY-MM-DD-<slug>.md`
- 人工评审 → `/nova-obs inbox-to-areas` 入库

## 为什么这么做（Why）

真实失败案例（2026-05-24）：
- 用户说"本次就直接入库"
- AI 实现：Write 4 份草稿到 `1-Inbox/` → 改 frontmatter → mv 到 `2-Areas/`
- 用户："我说的直接入库就是不让你直接进 inbox 了！！"

**根本逻辑**：
- "直接入库" = **省略 Inbox 中转**，不是"最终落到 2-Areas"
- 经 Inbox 中转 = 1-Inbox 出现一份 DRAFT 文件、frontmatter 是 `draft` 状态、再改字段、再 mv——3 次写入 + 1 次移动 + 必然要做反向链替换（DRAFT 名 → 正式名）
- 直接入库 = **1 次写入到 2-Areas**，frontmatter 一上来就是 `accepted/active`，不存在 DRAFT 名残留，反向链一次写正

经 Inbox 中转的成本：
- 2-Areas 文件 status 一开始就是 `active`，但 git 历史里这文件曾是 1-Inbox 的 DRAFT，造成认知噪音
- 反向链替换步骤（5.5）容易漏，留悬挂链接
- "直接"二字的承诺没兑现，用户感知混乱

## 反模式（Anti-patterns）

```text
❌ 反模式 1：用户说"直接入库" → AI 走 Write 1-Inbox + mv 路径
   表现：1-Inbox 短暂出现 DRAFT 文件、git diff 含 inbox 痕迹
   后果：用户判定 AI 没听懂

❌ 反模式 2：在 1-Inbox 写完后调 /nova-obs inbox-to-areas 当作"直接入库"
   表现：流程上看入库到了 2-Areas，但语义上经过了 inbox
   后果：违背"直接"承诺

❌ 反模式 3：用户没说"直接入库"，AI 自作主张 Write 直入 2-Areas
   表现：跳过人工评审节点
   后果：草稿质量没把关；红线"AI 直接写入 2-Areas"被违反
```

## 跨项目复用提示

- 任何带"草稿区→正式区"分阶段的工作流（PR draft / Notion inbox / wiki sandbox）都适用
- 用户用"直接 X"指令时，"直接"一般意味着**省略中间环节**，而不是"最终结果一致"

## 来源（Origin）

- 会话日期：2026-05-24
- 关键对话节选：
  > 用户："开始吧，修改obs技能的红线，以后允许直接入库（需要我特别说直接入库，否则还是按照inbox）。本次就直接入库"
  > AI 误读：Write 1-Inbox → mv 2-Areas
  > 用户："我说的直接入库就是不让你直接进inbox了！！"
  > AI 修正：Write 直入 2-Areas，沉淀本 PAT

## 关联

- 双通道铁律：[[PAT-17-obs-memory-split|PAT-17]]
- skill 红线开口：`.claude/skills/nova-obs {cur,adr,patterns,glossary,resource,memory}-to-inbox/SKILL.md`
- 参考 ADR：[[ADR-038-ui-depth-factor-rebalance|ADR-038]] / Pattern：[[PAT-83-canvas-sortingorder-overflow-clamp|PAT-83]]（本次会话其余直接入库案例的样本）
