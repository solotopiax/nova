---
id: PAT-52
title: CC 与 obs 联动四层强制机制
summary: hook + rules + 关键字四层联动
category: ai-collab
type: pattern
status: active
date: 2026-05-21
aliases:
  - PAT-52-cc-obs-four-layer-enforcement
keywords:
  - CC 与 obs 联动四层强制机制
  - PAT-52
  - PAT-52-cc-obs-four-layer-enforcement
tags:
  - pattern
  - nova
  - claude-code
  - obs
  - hooks
related:
  - "[[PAT-16-obs-keyword-trigger|PAT-16]]"
  - "[[PAT-25-rules-obs-patterns-collaboration|PAT-25]]"
  - "[[PAT-54-obs-rules-pre-action-lookup|PAT-54]]"
---

# PAT-52：CC 与 obs 联动四层强制机制

## 适用场景

- 需要让 Claude Code 会话在动手改代码 / 改文件时**机器强制**对齐 Obsidian 知识库（ADR / PAT / Glossary）
- 已有 obs 沉淀但 AI 凭记忆漏读规范、被用户反复指认才回查的场景
- 想杜绝 AI「事后核对」式使用 obs，要求「事前对齐」零疏漏

## 核心做法

四层联动，全部为机器强制（AI 无法绕过）：

| 层 | 触发时机 | 落地物 | 作用 |
|---|---|---|---|
| L0-pre | AI 调 Edit/Write/MultiEdit **之前** | `.claude/hooks/nova-rules-pretooluse.sh` | 按文件路径分发注入对应 PAT 全文 |
| L0-post | AI 完成 Edit/Write/MultiEdit **之后** | `.claude/hooks/nova-rules-posttooluse.sh` | 反模式 grep 字符比对，命中即报警 |
| L1 | 路径匹配 `paths:` | `.claude/rules/framework-inspector-alignment.md` 等 | Claude 规则自动注入 |
| L2 | 用户消息含关键字 | `.claude/hooks/nova-obs-keyword-guard.sh` | UserPromptSubmit 关键字白名单（含 `obs`） |

关键设计：

1. **PreToolUse + matcher `Edit|Write|MultiEdit`**：仅在文件落盘时刻触发，避免 Read/Grep 等只读工具误触发。
2. **PostToolUse 反模式 grep**：用正则字符匹配（如 `GUILayout\.Width\(17[0-9]\)`、`IncreaseIndentLevel`、裸 `EditorGUILayout`）做客观校验，不依赖 AI 主观判断。
3. **路径分发**：Inspector / Runtime / Editor / Minds 各路径注入不同的 PAT 索引节，覆盖各模块铁律。
4. **扩展点**：新增违规检测只往 PostToolUse grep 表加一条正则即可。

动手三问（写在 rules 末尾，AI 必读）：

1. 路径在 `.claude/rules/` 是否有 `paths:` 命中？命中即重读全文
2. rule 末尾「关联」节列了哪些 PAT？全部去 `0-Index-PAT.md` 拉
3. 是否有协同 SOP 类 PAT 统辖？（Inspector→PAT-31 / Runtime→PAT-32 / SDK→PAT-33）先看协同节

## 反模式

- ❌ 只布 L1 (rules `paths:`) + L2 (UserPromptSubmit 关键字)：AI 自己改文件时 UserPromptSubmit 不触发，rules 又不强制读全文，全靠记忆 → 必漏
- ❌ 把 obs 当「事后核对工具」而非「事前对齐工具」
- ❌ rule 文件只搬一条 PAT 就当覆盖完整（如只搬 PAT-24 漏掉 PAT-09/21/31 协同）
- ❌ matcher 写 `"*"`：所有工具都触发，Read 等只读工具会被拖慢且无意义
- ❌ 反模式检测靠 AI 自查而非 grep：主观判断必有疏漏

## 来源对话摘录

> 「你现在的方案是通过钩子触发的吗？我要的是万无一失，不仅仅是针对刚才处理的这个问题，我之前一直强调 cc 与 obs 之间的联动性，必须联动起来，万无一失，严格禁止任何疏漏」

> 「根本性漏洞：AI 自己改 Inspector 文件时，UserPromptSubmit hook 没机会触发，rules 自动注入又不强制阅读规范全文，全靠 AI『应该记得』。这就是这次失误的机器层根因。」

> 「PostToolUse 当场就抓出了 ProcedureComponentInspector.Methods.cs 还有两处违规：第 161/184 行的 IncreaseIndentLevel 和第 177-180 行的裸 EditorGUILayout——这恰恰证明机器层防线管用。」

> 「matcher `Edit|Write|MultiEdit` 的设计意图：只在文件落盘时刻触发规则检查，避免无关工具（如 Read）误触发拖慢响应。」

---
