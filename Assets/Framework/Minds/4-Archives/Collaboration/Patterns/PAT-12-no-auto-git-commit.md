---
id: PAT-12
title: AI 协作不自动 git commit / push
type: pattern
status: active
date: 2026-05-16
summary: Nova项目不自动git commit由用户掌握
category: workflow
aliases:
  - PAT-12-no-auto-git-commit
keywords:
  - PAT-12
  - PAT-12-no-auto-git-commit
  - AI 协作不自动 git commit / push
tags:
  - pattern
  - workflow
  - git
  - ai-collab
related:
  - "[[PAT-06-main-session-dispatch|PAT-06]]"
---

# PAT-12：AI 协作不自动 git commit / push

## 适用场景（When）

- AI（Claude / Copilot / Cursor 等）参与代码 / 文档 / 设计写作的项目
- 项目有完整人工 review 流程（PR 评审 / 上线节点 / 合规检查）
- AI 同时产出代码 + 设计稿 + memory + 临时草稿等多类产物
- 团队希望 git 提交节点完全由人控制

## 核心做法（What & How）

### 基本铁律

**Nova 项目内：写完文件就停下，不自动执行 `git commit` / `git push`。**

涵盖：
- 代码改动（.cs / .asmdef / Prefab / .meta）
- 设计文档（spec / plan / brainstorm 产物）
- Memory / 配置（.claude / settings.json）
- AI 起草的 obs 草稿（1-Inbox/）

### 例外触发条件

只有用户消息明确出现以下字样才执行 git 操作：

| 字样 | 触发动作 |
|---|---|
| 「提交」/「commit」 | `git commit` |
| 「推」/「push」 | `git push` |
| 「打 tag」 | `git tag` |
| 「合到 main / merge」 | 走合并流程 |

> [!tip] 模糊措辞不触发
> 「保存」/「写完」/「OK」等模糊措辞不触发 git 操作。

### Skill 集成

- `brainstorming` / `writing-plans` / `subagent-driven` 等技能里"commit the design document to git"步骤在此项目自动跳过
- 写完文件 → 报告路径 → 进入 user review gate（停下等用户决定）

### Specs / 设计文档专项

`docs/superpowers/specs/YYYY-MM-DD-*.md` 等设计文档明确**不进 git**：
- AI 起草后只 `Write` 文件
- 不 `git add` / `git commit`
- 也不放进 `.gitignore`（保留为本地草稿）

## 为什么这么做（Why）

- **commit 节点是审计点**：每个 commit 是 git 历史的一道刻度，应当对应一个有意义的人工决策
- **AI 节奏 ≠ 人节奏**：AI 一次会话可产出十几个文件改动，但用户审单可能要分多次；自动提交会把"中间快照"凝固成历史
- **避免半成品入库**：AI 阶段性产物可能未经 review 就进 main 分支，污染团队协作
- **特殊产物保护**：设计文档 / memory / 草稿 这类「思考产物」不属于代码资产，不应进 git
- **历史源头**：用户 2026-04-28 ConfigWindow 设计会话原话："不要提交 git，以后也不需要提交 git"

## 反模式（Anti-patterns）

- **AI 写完代码顺手 `git commit -m "AI auto commit"`**：commit 信息无意义，节点未经人审
- **跑测试通过后自动 commit**：测试通过 ≠ 用户认可，可能用户还想再调整
- **每个 spec 都进 git**：设计文档膨胀十几 KB，污染主仓 history
- **借口"反正能 reset"**：reset 之后日志仍在 reflog 中，且团队仓库一旦 push 就无法 reset
- **看见模糊措辞就 commit**：用户说"OK 看着不错"不是"提交"
- **Skill 默认行为不改**：通用 skill 自带 commit 步骤，移植到 Nova 项目要单独豁免

## 跨项目复用提示

- **思想完全可迁移**：所有人 AI 协作项目都应明确 git 触发权限归属
- 推荐做成项目级 `CLAUDE.md` / `AGENTS.md` 铁律，让所有 AI 工具读到
- 团队规模决定豁免边界：单人项目可适当放宽（"AI 改完代码自动 commit + 起 PR 让我审"），团队项目应严格人工控制
- 不适合的场景：纯 AI 自动化流水线（如 Renovate bot 的依赖更新）——这类自动 commit 有明确 scope 是另一类范式
- 关键工具：`.gitconfig` 钩子、Husky pre-commit、Skill 内置开关都能辅助

## 关联

- Memory 指针：`feedback_no_git_commit.md`
- 历史源头：2026-04-28 ConfigWindow 设计会话用户原话
- 相关 Skill：`superpowers:brainstorming` / `writing-plans` 的 commit step 在 Nova 项目跳过
