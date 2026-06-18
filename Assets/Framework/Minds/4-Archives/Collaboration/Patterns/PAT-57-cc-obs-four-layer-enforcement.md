---
id: PAT-57
title: CC 与 obs 联动的四层强制机制
type: pattern
status: active
date: 2026-05-21
summary: hook + rules + 关键字守卫四层兜底
category: ai-collab
aliases:
  - PAT-57
  - PAT-DRAFT-2026-05-21-cc-obs-four-layer-enforcement
keywords:
  - CC 与 obs 联动的四层强制机制
  - PAT-57
tags:
  - pattern
  - methodology
  - ai-collab
  - claude-code
  - obs
  - hooks
---

# PAT-57：CC 与 obs 联动的四层强制机制

## 适用场景

- 需要把 obs 知识库（ADR/PAT）与 Claude Code 工作流强绑定，确保 AI 改代码前后必读必查规范
- 当出现「obs 已沉淀但 AI 没读」类失误时，需要从「AI 自觉」升级为「机器强制」
- 适用于任何依赖规范沉淀的 CC 项目：Inspector 绘制、Runtime 模块、SDK 接入等场景

## 核心做法

四层联动，每层都不可被 AI 绕过：

| 层 | 触发时机 | 落地物 | 作用 |
|---|---|---|---|
| **L0-pre** | AI 调 Edit/Write/MultiEdit **之前** | `.claude/hooks/nova-rules-pretooluse.sh` | 按路径分发注入对应 PAT 全文 |
| **L0-post** | AI 调 Edit/Write/MultiEdit **之后** | `.claude/hooks/nova-rules-posttooluse.sh` | 反模式 grep 字符级自检 |
| **L1** | 路径 `paths:` 匹配时 | `.claude/rules/*.md` | rules 自动注入完整规范清单 |
| **L2** | 用户消息含「规范/规则/obs」等关键字 | `.claude/hooks/nova-obs-keyword-guard.sh` | 关键字 reminder 双通道落地 |

关键设计：

1. **matcher = `Edit|Write|MultiEdit`**：竖线为正则「或」，仅在文件落盘时刻触发，避免 Read/Grep 误触
2. **PreToolUse 按路径分发**：`Editor/Inspectors/**` → 注入 PAT-09/20/21/24/31；`Runtime/Modules/**` → 注入 Component+Manager 三层链；`Editor/**`（非 EditorUtil）→ 注入 PAT-35/39
3. **PostToolUse grep 字符级比对**：如 `GUILayout\.Width\(17[0-9]\)` 命中即提示改 `180f`；`IncreaseIndentLevel|DecreaseIndentLevel` 命中即改 `Horizontal+Space(16f)`
4. **rules 内容必须完整**：不能只搬一条 PAT（如只搬 PAT-24 而漏 PAT-09/21/31），否则 hook 触发了也漏关键条款
5. **新增违规检测的扩展点**：往 PostToolUse 脚本加一行 grep 即可，不改主流程

## 反模式

- ❌ **只靠 AI 自觉「动手三问」**：依赖 AI 记忆，不可信，必失守
- ❌ **只布 UserPromptSubmit hook**：用户不主动问就不触发，AI 自己改文件时无卡点
- ❌ **rules 文件内容残缺**：只搬冰山一角导致即使读了规则也漏关键条款（本次失误的 R2 根因）
- ❌ **把 obs 当事后核对工具**：必须当事前对齐工具，PreToolUse 是关键杠杆
- ❌ **沿袭周边老代码反模式**：原文件含 `175`/`IncreaseIndentLevel` 直接对齐周边，违反 PAT-39「先查再扩再用」
- ❌ **matcher 写 `"*"` 滥触发**：拖慢响应且日志噪音大；应精确锁定文件落盘工具

## 来源对话摘录

> 用户：「分析一下是什么原因没有关联到 obs 的规范，这个流程是必须遵守和重视的内容，找出原因，给出日后永远不会再犯 obs 约定的修改方案！」

> 用户：「你现在的方案是通过钩子触发的吗？我要的是万无一失，而是不仅仅是针对刚才处理的这个问题，我之前一直强调 cc 与 obs 之间的联动性，我的要求是必须联动起来，万无一失，严格禁止任何疏漏」

> 复盘：「不是 obs 失效，是我没读 obs。PAT-09 / PAT-21 / PAT-24 / PAT-31 早已完整沉淀」「之前那个方案只靠 AI 自觉 + UserPromptSubmit 关键字 hook，是漏的——AI 自己改文件时没有机器层强制」

> 验证：PostToolUse hook 当场抓出 `ProcedureComponentInspector.Methods.cs` 第 161/184 行 `IncreaseIndentLevel` 和第 177-180 行裸 `EditorGUILayout` 违规，证明机器层防线管用

---
