---
id: PAT-60
title: 规则文件部分推翻时按节迁移并重命名去过时前缀
summary: 老文件别原地修，按节抽幸存条款迁新名重写
category: workflow
type: pattern
status: active
date: 2026-05-21
aliases:
  - PAT-60-rules-file-section-migration
keywords:
  - PAT-60
  - PAT-60-rules-file-section-migration
  - 规则文件部分推翻时按节迁移并重命名去过时前缀
tags:
  - pattern
  - rules
  - refactor
  - naming
  - ai-collab
related:
  - "[[PAT-54-obs-rules-pre-action-lookup|PAT-54]]"
---

# PAT-60：规则文件部分推翻时按节迁移并重命名去过时前缀

## 适用场景（When）

`.claude/rules/<xxx>.md` 中的某一节（或大半内容）被新决策推翻，但**仍有若干节继续有效**：

- 不能整删（剩余条款仍是当前铁律）
- 不能原地编辑（文件名 / 节编号 / 标题前缀已经误导 AI 与人）
- 直接把废节注释为"已废止"会让规则文件累积"墓碑"，未来读规则时还要绕过墓碑判断

## 核心做法（What & How）

1. **逐节判定三档**：仍有效（保留）/ 已废止（删除）/ 重复其他规则（剔除）。
2. **检查文件命名**：旧名是否带场景前缀（如 `framework-hotupdate-constraints.md` 的 `hotupdate-`）？若幸存条款已**超出**该场景，文件名同步改名（如改为 `framework-hybridclr-constraints.md`）。
3. **整体新建**而非原地编辑：
   - 删除旧文件（不留墓碑、不留兼容跳板）
   - 新建命名更准的文件，把"保留"档条款按新顺序重写
   - 同步更新 `.claude/CLAUDE.md` 规则按需加载表 + `.claude/hooks/nova-rules-pretooluse.sh` 的提醒文案
4. **新增对应 ADR 草稿**记录决策来源 + supersedes 旧 ADR/GLO 编号，让扫描器能识别取代链路。

## 为什么这么做（Why）

- **原地编辑会被 AI 反复重读**：旧文件名 / 旧节标题被 hook 注入或被规则索引指向，每次新会话 AI 都会按旧前缀理解新内容，造成"规则名字说 A，实际只剩 B"的认知错位。
- **墓碑式废止条款是污染**：`# §二（已废止）` 这种留底未来会被新人 AI 误读，反而要花更多 token 解释"为什么这条被划掉但还在文件里"。
- **场景前缀失真后果严重**：如本会话踩到的 `framework-hotupdate-constraints.md` 实际包含一半"HybridCLR 通用约束"（程序集命名、加载链路、Procedure 分档），不只是热更；前缀让人误以为"非热更场景可忽略"。

## 反模式（Anti-patterns）

- 在原文件顶部加「⚠️ 本文件部分条款已废止，详见 ADR-XXX」——AI 不会因此跳读，墓碑+幸存条款混居反而误导
- 改文件名但保留旧节编号（"§二保留 §三删除"）——节号是历史包袱，新文件应重新编号
- 新增 PAT 但不删旧规则文件——规则文件被认定为权威源，PAT 改不动它

## 跨项目复用提示

可直接复用。任何"部分推翻历史规则文档"的场景都适用，不限于 Nova。但需确保项目里：
- 规则文件位置统一（如本项目 `.claude/rules/`）
- 有规则索引机制（如本项目 `.claude/CLAUDE.md` 的"规则按需加载"表 + hook 提醒）
否则新文件加进去没人引用等于白写。

## 来源（Origin）

- 会话日期：2026-05-21
- 关键对话节选：
  > 用户：「废弃 framework-hotupdate-constraints.md 这个约束，删除相关代码和文档」
  > 用户：「需要你 review 一下被废弃的这个 framework-hotupdate-constraints.md 中哪些内容是可以留下来的？」
  > AI 按节列出"保留 / 废止 / 重复"三档 → 新建 `framework-hybridclr-constraints.md`（去掉 `hotupdate-` 前缀），删除旧文件
  > 用户：「§三中提到的 HotfixComponent 已经不在了，请修改」（说明老文件还有过时事实需要按当前代码核对）

## 关联

- 相关 ADR：[[ADR-032-drop-novabehaviour-bridge|ADR-032]]（触发本次规则文件迁移）
- 相关 Pattern：[[PAT-54-obs-rules-pre-action-lookup|PAT-54]]（改前必扫 obs+rules，迁移后必须刷新规则索引才能让 PAT-54 生效）
