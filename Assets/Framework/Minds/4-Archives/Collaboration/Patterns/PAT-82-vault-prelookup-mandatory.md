---
id: PAT-82
title: 全 agent Vault 前置查询铁律（接活前 4 步硬约束）
summary: 接活先grep Vault 不查不动手 报告顶部汇报匹配
category: ai-collab
status: active
date: 2026-05-24
aliases:
  - PAT-82-vault-prelookup-mandatory
  - vault-prelookup
  - 前置查询铁律
tags:
  - pat
  - nova
  - ai-collab
  - workflow
  - obs
  - dispatch
  - governance
supersedes: []
superseded-by: []
related:
  - "[[ADR-003-main-session-as-team-leader|ADR-003]]"
  - "[[ADR-034-architect-mandatory-prerequisite|ADR-034]]"
  - "[[PAT-06-main-session-dispatch|PAT-06]]"
  - "[[PAT-17-obs-memory-split|PAT-17]]"
  - "[[PAT-57-cc-obs-four-layer-enforcement|PAT-57]]"
  - feedback_obs_trigger_on_rule_keywords
  - feedback_obs_rules_pre_action_lookup
---

# PAT-82：全 agent Vault 前置查询铁律

## 适用场景

任何 Nova agent（runtime-coder / editor-coder / code-reviewer / qa-reviewer / doc-writer）在**接到任务后、动手前**，必须先在 Obsidian Vault 中检索历史决策与强制术语，对齐已有约束再开工。原 architect.md 已具备此节，但落到 5 个执行类 agent 时一直靠 keyword-guard hook 被动触发，存在"AI 接活直接动手 → keyword 没命中 → 违规无察觉"的漏洞。本 PAT 把"前置查询"升级为**全 agent 入口硬约束**。

## 痛点（Why）

- **症状**：AI 接到需求 → 凭印象写代码/审查/写文档 → 输出后才发现违反 Vault 已有 ADR、PAT、强制术语；用户被迫反复纠正
- **根因**：架构上只有 architect.md 写了"Vault 前置查询（强制）"，5 个执行类 agent 没有同等约束
- **过往兜底失败**：keyword-guard hook 仅扫用户消息中的"规范/规则"等词；用户用业务语言提需求时（如"加个新 UI 面板"）不命中关键字，hook 沉默 → AI 直奔代码 → 违规

## 核心做法（What）

### 一、4 步法（任何 agent 接活后必走）

```text
1. 提关键字
   └ 从需求中提取：模块名 / Manager 名 / 反模式词 / 强制术语
2. grep 历史决策
   └ grep -rln "<关键字>" \
       "${CLAUDE_PROJECT_DIR}/Assets/Framework/Minds/2-Areas/" \
       "${CLAUDE_PROJECT_DIR}/Assets/Framework/Minds/0-Index/"
3. Read 命中条目
   └ 每 hit 记录：id + status (accepted/superseded) + 一句要点
   └ 同步扫 0-Index-Terms.md 全文，建立强制术语 + 反模式词清单
4. 报告顶部汇报【知识库前置匹配】节
   ├ 命中：[ADR-XXX] / [PAT-NN] / [GLO-NN] 要点 + 本次如何遵循
   ├ 无命中：显式标注「Vault 无相关历史决策」
   └ 强制术语命中：列出本次涉及术语，违反则 REJECT
```

### 二、各 agent 的 4 步重点（差异化）

| Agent | grep 重点目录 | 强制术语校验 | 备注 |
|---|---|---|---|
| **runtime-coder** | 2-Areas/ADR/ + 2-Areas/Patterns/ + 2-Areas/Glossary/ | 模块命名 / 反模式词 / Asset 地址 | 改 cs 前先扫 |
| **editor-coder** | 2-Areas/ADR/ + 2-Areas/Patterns/ | UI 文案规范 / EditorPrefs / EditorGUI* 反模式 | 改 Inspector / EditorWindow 前先扫 |
| **code-reviewer** | 全 2-Areas/ + 0-Index-* | 全部强制术语 + 反模式词 | 审 diff 前必扫；diff 触敏需 Read ADR 全文 |
| **qa-reviewer** | 2-Areas/ADR/ + 2-Areas/Glossary/（不读 Patterns） | 仅"预期行为 / 不变量"定义 | 把 ADR / GLO 摘进测试判定条件 |
| **doc-writer** | 0-Index-Terms.md（必读全文）+ 2-Areas/Glossary/ | 全部强制术语 + 命名约束 | 文档措辞以 Vault 为权威源 |

### 三、冲突优先级

```text
Vault ADR > .claude/rules/*.md > Assets/Framework/Docs/
```

冲突时以 ADR 为准，并在产出中提示"规则文件已过期"。

### 四、机器强制层

| 层 | 机制 | 兜底范围 |
|---|---|---|
| **agent.md「Vault 前置查询」节** | 6 个 agent.md 全部包含；接活时 LLM 必读 | 一线主防线 |
| **CLAUDE.md「前置类」红线** | 主会话级硬约束；4 条违反判定 | 调度审核线 |
| **keyword-guard hook RULE/PRELOOKUP 双触发** | UserPromptSubmit 命中模块名/反模式词/术语即注入 reminder | 用户消息侧硬注入 |
| **派活 prompt【强制前置查询】节** | team-leader.md 规定主会话派活时强制注入 | 子 agent prompt 硬约束 |

四层联动覆盖：用户提需求 → 主会话调度 → 派活 prompt → 子 agent 接活，任一环节缺失都会被下一层兜住。

## 反模式（Don't）

- ❌ "我熟悉这个模块，跳过 grep 直接动手" —— Vault 决策可能已迭代，记忆是过时缓存
- ❌ 报告产出顶部缺【知识库前置匹配】节 —— 即使做了查询也违反流程红线
- ❌ Vault 命中条目却不在产出中说明遵循 / 推翻 / 部分违反关系 —— 等于没查
- ❌ 强制术语命中却写"资源地址"/"Panel"/"Window" —— 0-Index-Terms.md 已明令禁止
- ❌ 子 agent 主动跳过：派活 prompt 没注入【强制前置查询】节就动手 —— 主会话派活违规

## 与既有 PAT 的关系

- **PAT-06 主会话调度**：本 PAT 是「主会话派活时必须在 prompt 中注入的硬约束」之一
- **PAT-17 obs ↔ memory 分工**：本 PAT 是 obs 知识"被消费"的规则；PAT-17 管"如何写"，本 PAT 管"如何用"
- **PAT-57 cc-obs 四层强制**：本 PAT 是 PAT-57 的执行落点之一（"前置查询"是四层中的"用知识"层）
- **feedback_obs_rules_pre_action_lookup**：memory 短指针，本 PAT 是其全文沉淀

## 落地清单

- [x] `.claude/agents/runtime-coder.md` 加「Vault 前置查询（强制，不可跳过）」节
- [x] `.claude/agents/editor-coder.md` 加同上
- [x] `.claude/agents/code-reviewer.md` 升级原节，强制 4 步 + 报告顶部汇报
- [x] `.claude/agents/qa-reviewer.md` 加轻量版（仅 ADR / GLO）
- [x] `.claude/agents/doc-writer.md` 加轻量版（必读 0-Index-Terms.md）
- [x] `.claude/CLAUDE.md` 红线表加「前置类」节
- [x] `.claude/hooks/nova-obs-keyword-guard.sh` 增 PRELOOKUP_KEYWORDS 数组
- [x] `.claude/agents/team-leader.md`「派活 Prompt 必备节」+ 模板补充

## 验证

- 用户提非规范类需求（如"加个 UI 面板"）→ keyword-guard PRELOOKUP 命中"UIView" → 注入 reminder
- 主会话派 runtime-coder → prompt 含【强制前置查询】节 → coder 报告顶部出现【知识库前置匹配】节
- code-reviewer 审查 diff → 报告含「ADR-XXX 合规」/ Vault 无相关历史决策 标注
- 缺失任一节 = 流程红线违反，主会话退回重做

## 关联

- 上游：[[ADR-003-main-session-as-team-leader|ADR-003]]（主会话即调度中心）
- 派生：[[ADR-034-architect-mandatory-prerequisite|ADR-034]]（architect 强制前置，本 PAT 把同等强制扩展到执行类 agent）
- Pattern：[[PAT-06-main-session-dispatch|PAT-06]]、[[PAT-17-obs-memory-split|PAT-17]]、[[PAT-57-cc-obs-four-layer-enforcement|PAT-57]]
- Memory：`feedback_obs_rules_pre_action_lookup`、`feedback_obs_trigger_on_rule_keywords`
- 配置：`.claude/CLAUDE.md`、`.claude/agents/*.md`、`.claude/hooks/nova-obs-keyword-guard.sh`
- 索引：[[0-Index-FolderGuide|0-Index-FolderGuide]]、[[0-Index-Terms|0-Index-Terms]]
