---
id: PAT-56
title: 团队共享命令入 .claude/CLAUDE.md，个人偏好留 CLAUDE.local.md
summary: 团队命令入 git；.local.md 仅放个人偏好
category: ai-collab
type: pattern
status: active
date: 2026-05-21
aliases:
  - PAT-56-team-vs-personal-claude-md-split
keywords:
  - PAT-56
  - PAT-56-team-vs-personal-claude-md-split
tags:
  - pattern
  - claude-code
  - ai-collab
  - workflow
  - git
related:
  - "[[ADR-029-claude-md-locate-in-dot-claude|ADR-029]]"
  - "[[ADR-030-tools-publish-to-cc-skill|ADR-030]]"
---

# PAT-56：团队共享命令入 .claude/CLAUDE.md，个人偏好留 CLAUDE.local.md

## 适用场景（When）

- Nova 项目使用 Claude Code 的双层配置：
  - `.claude/CLAUDE.md`：项目级配置，**入 git**，团队所有成员可见。
  - `CLAUDE.local.md`：个人级配置，**`.gitignore` 屏蔽**（项目根 `.gitignore:107`），仅本机生效。
- 当需要落地一条「调用某命令 / 跑某脚本 / 访问某基础设施」的指引时，必须先判断它是团队共享还是个人私货。

## 核心做法（What & How）

**团队共享，入 `.claude/CLAUDE.md`：**

- 项目工作流命令：UPM 发布、构建、回归、配置导出等。
- 团队基础设施地址（除非含密钥）：私域 npm 注册表、内网 MCP 服务地址、构建机入口等。
- 团队规则、流水线步骤、agent 调度约定。

**个人私货，留 `CLAUDE.local.md`：**

- 个人沟通偏好（语言、详略、风格）。
- 个人快捷命令别名 / 本机额外路径。
- 个人临时调试技巧（与团队规范无关）。
- **不**放任何「别人不知道就出错」的内容。

**判定问题**：删除该条目后，新加入团队的成员能否独立完成这件事？

- 不能 → 入 `.claude/CLAUDE.md`
- 能（只是不舒服）→ 留 `CLAUDE.local.md`

## 为什么这么做（Why）

会话踩坑：UPM 发布命令最初只写在 `CLAUDE.local.md`，被 `.gitignore` 屏蔽，团队其他人完全看不到入口。本以为「本地命令本地放」是对的，实际是把团队基础设施当成了个人偏好。

`CLAUDE.local.md` 的「local」语义是**个人不愿暴露给团队**（沟通风格、个人偏好），而不是**只在我这台机器执行**。命令的执行位置（本机）与命令的归属（团队/个人）是两回事，不能混。

## 反模式（Anti-patterns）

- 把发布脚本路径、Verdaccio 地址、构建命令塞 `CLAUDE.local.md`——团队复刻不了。
- 把个人沟通偏好（"用中文"、"回复简洁"）塞 `.claude/CLAUDE.md`——给团队加私货。
- 一份命令两处都写——后续路径迁移漏改一处就发散。
- 路径迁移（如 `Tools/Publish/` → `.claude/skills/nova-publish/`）后只改其中一处，另一处变陈尸。

## 跨项目复用提示

适用所有使用 Claude Code 双层配置（`<project>/.claude/CLAUDE.md` + `<project>/CLAUDE.local.md`）的项目。本规则与 Nova 业务无关，纯协作约定，可直接搬用。

注意：是否启用 `CLAUDE.local.md` ignore 由项目 `.gitignore` 决定，搬用时先确认 ignore 模式是否生效，避免「以为是私货实际入了 git」。

## 来源（Origin）

- 会话日期：2026-05-21
- 关键对话节选：
  > AI（错）："`CLAUDE.local.md` 那条只是个人快捷提醒，可直接删除（避免重复维护两处路径）"
  > 用户："为什么新 skill 路径（本地文件，不入 git）？我要走版本管理"
  > AI 落地动作：把 UPM 发布命令从 `CLAUDE.local.md` 搬到 `.claude/CLAUDE.md`「UPM 发布」节，团队可见。

## 关联

- 相关 ADR：[[ADR-029-claude-md-locate-in-dot-claude|ADR-029]]（CLAUDE.md / CLAUDE.local.md 双双驻 .claude/）；[[ADR-030-tools-publish-to-cc-skill|ADR-030]]（发布工具迁移诱因）。
- 相关 Pattern：暂无。
