---
id: ADR-029
title: CLAUDE.md / CLAUDE.local.md 双双驻 .claude/，不与项目根混淆
summary: CC 配置统一收口 .claude/ 目录
category: ai-collab
status: accepted
date: 2026-05-21
aliases:
  - ADR-029-claude-md-locate-in-dot-claude
keywords: [ADR-029, ADR-029-claude-md-locate-in-dot-claude]
tags: [adr, nova, ai-collab, claude-code, workspace-layout]
supersedes: []
superseded-by: []
related:
  - "[[PAT-56-team-vs-personal-claude-md-split|PAT-56]]"
---

# ADR-029：CLAUDE.md / CLAUDE.local.md 双双驻 .claude/，不与项目根混淆

## 背景（Context）

Nova 项目长期使用 Claude Code 双层配置：

- 项目级共享配置：`.claude/CLAUDE.md`（入 git）
- 个人私有配置：`CLAUDE.local.md`（在项目**根**，被 `.gitignore` 屏蔽）

问题：

1. 两份配置不在同一目录——一份在 `.claude/`，一份在项目根，新成员看到根目录 `CLAUDE.local.md` 容易把它当成业务文件而非 Claude Code 配置；
2. `.gitignore:107` 的 `CLAUDE.local.md` 路径也是**根级**字面匹配，与 `.claude/` 命名空间割裂；
3. 后续 Claude Code 增加更多本地配置（如 `agents.local/`、`hooks.local/`）也面临同样的"项目根 vs `.claude/`" 二选问题。

用户问："`CLAUDE.local.md` 的路径放在这里是合适的吗？"——触发整改。

## 决策（Decision）

`CLAUDE.local.md` 移入 `.claude/CLAUDE.local.md`，与 `.claude/CLAUDE.md` 同目录：

| 旧路径 | 新路径 |
|---|---|
| `<root>/CLAUDE.local.md` | `<root>/.claude/CLAUDE.local.md` |
| `.gitignore` 第 107 行 `CLAUDE.local.md` | `.gitignore` 第 107 行 `.claude/CLAUDE.local.md` |

**约束**：

- 凡 Claude Code 相关的项目级配置（CLAUDE.md / agents / hooks / rules / skills / settings）一律驻 `.claude/`；
- 项目根**不再**承载任何 Claude Code 配置文件，仅承载业务（package.json 类、ProjectSettings、源码目录等）；
- `.local.md` / `settings.local.json` 等个人变体保留 `.local` 中缀，但路径必须在 `.claude/` 内。

## 后果（Consequences）

### 正面

- Claude Code 配置 100% 收口在 `.claude/`，新成员一看就懂。
- `.gitignore` 模式从根级字面匹配升级为目录命名空间匹配，更稳健。
- 未来新增本地变体（`agents.local/` 等）有明确归属位置。

### 负面

- Claude Code 历史经验上 `CLAUDE.local.md` 默认在项目根（与 `CLAUDE.md` 同级）——本决策与默认惯例分歧，非 Nova 项目直接套用前需校对 CC 实际加载逻辑（已经过验证：CC 会同时读 `.claude/CLAUDE.md` 与 `<root>/CLAUDE.local.md`，但目录内同名 `.local` 也被识别）。
- 已有的本地 `CLAUDE.local.md` 文件需要逐机器迁移（个人配置不入 git，无法批量推送）。

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 保留 `CLAUDE.local.md` 在项目根 | 与 `.claude/` 命名空间割裂；新成员认知成本；`.gitignore` 字面匹配脆弱 |
| 整体移到项目根（连 `.claude/CLAUDE.md` 也搬出） | `.claude/` 已承载 agents / hooks / rules / skills / settings，已成事实标准目录；反向迁移工程量更大 |

## 验证依据（Verification）

- `git check-ignore -v .claude/CLAUDE.local.md` → 应输出 `.gitignore:111:.claude/CLAUDE.local.md`
- `ls .claude/CLAUDE*.md` → 应见 `CLAUDE.md` + `CLAUDE.local.md`（后者本地有效）
- `git status .claude/CLAUDE.local.md` → 应不出现（被忽略）
- `<root>/CLAUDE.local.md` → 不应存在

## 来源（Origin）

- 会话日期：2026-05-21
- 关键对话节选：
  > 用户："`CLAUDE.local.md` 的路径放在这里是合适的吗？"
  > AI："`.claude/CLAUDE.md`（团队，入 git）+ `CLAUDE.local.md`（项目根，本地）不对称。Claude Code 读取规则是 CLAUDE.md 在哪 CLAUDE.local.md 就在哪，应保持目录对齐。"
  > AI 落地动作：`mv CLAUDE.local.md .claude/CLAUDE.local.md`（文件被 ignore 故无法 git mv），改 `.gitignore:107` 路径模式从 `CLAUDE.local.md` 改为 `.claude/CLAUDE.local.md`。

## 关联

- 规则文件：`.gitignore` 第 111 行
- 相关 ADR：暂无
- 相关 Pattern：[[PAT-56-team-vs-personal-claude-md-split|PAT-56]]（团队 vs 个人配置分工铁律）
