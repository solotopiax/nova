---
id: PAT-47
title: AI 主动执行型 skill 调用时禁止再问形式性确认
type: pattern
status: active
date: 2026-05-19
summary: AI主动执行型skill禁问形式性确认
category: ai-collab
aliases:
  - PAT-47-ai-skill-no-redundant-confirm
keywords:
  - AI 主动执行型 skill 调用时禁止再问形式性确认
  - PAT-47
  - PAT-47-ai-skill-no-redundant-confirm
tags:
  - pattern
  - ai-discipline
  - ux
  - skill-design
  - methodology
related:
  - "[[PAT-14-plan-to-subagent-driven|PAT-14]]"
  - "[[PAT-26-ai-concise-reply|PAT-26]]"
---

# PAT-47：AI 主动执行型 skill 调用时禁止再问形式性确认

## 适用场景（When）

- AI 自己起草的内容（如 obs 草稿、PR 描述、commit message 草案、Plan）紧接着由 AI 自己调 skill 落地
- skill 模板内置「人工评审完了吗？(y/n)」「确定要执行？(y/n)」类二次确认问题，但**实际工作流里没有"人工评审"这一步独立存在**——内容刚由 AI 自己写完
- 用户已表态「直接做 / 不用问」类偏好（如 [[PAT-12-no-auto-git-commit|PAT-12]] 反向，本 Pattern 关注的是 AI 应主动执行的场景）

典型触发信号：用户对同类 skill 重复说出「以后不要再问 / 直接做就行 / 这种确认没必要」。

## 核心做法（What & How）

### 区分两类 skill 二次确认

| 类型 | 是否合理 | 处理 |
|---|---|---|
| **真正破坏性 / 不可逆操作**（git push / git reset --hard / 删除 ARC 入归档区） | 合理 | 保留确认；用户必须为后果背书 |
| **形式性流程确认**（"评审完了吗" / "确认入库"——内容刚 AI 自己写完，无第三方评审介入） | **不合理** | AI 应跳过；用户调 skill 本身就等于授权执行 |

### AI 调用时的执行模板

调用形式性确认型 skill 时：
1. 直接走完整的"算编号 → 改字段 → 反向链回填 → 移动 → 更新索引 → 报告"
2. 真异常（编号冲突 / 字段缺失 / 反向链残留）才中止；让用户处理实际异常
3. 报告里给最终结果（路径、编号、Obsidian 链接），不给"准备入库 / 进度 N/M"之类过程播报

### Skill 设计侧反向修正（可选）

发现 skill 模板里有这种形式确认句时，可顺手把它从"硬阻塞 (y/n)"降级为"按需告知"——让 skill 默认就直接执行。

## 为什么这么做（Why）

- **AI 起草 + AI 落地 = 单一责任主体**：在这条链路上向用户问"评审完了吗"等于把 AI 自己的草稿质量责任反推给用户，没信息增量
- **形式确认会稀释真正的确认**：用户面对每次都要点 y 的确认句，会形成"无脑回 y"反射；下次真正破坏性操作来确认时，用户也可能本能回 y
- **打断主线节奏**：用户调 skill 本身就是执行授权，再问一遍是"对授权的二次确认"，违背 [[PAT-26-ai-concise-reply|PAT-26]]
- **质量把关在起草环节**：草稿是否合格应在写 frontmatter / 5 个核心问题时把关；入库环节再问形式上确认，门关错了位置

## 反模式（Anti-patterns）

- **「这是 skill 模板要求的我就照做」**：模板是为通用工作流写的；Nova 实际工作流（AI 全程主驱）下「评审完了吗」这一问无意义，AI 应识别上下文跳过
- **每份草稿都问一遍**：同会话里入库 5 份草稿、问 5 次确认 → 用户必然爆。批量入库 skill 即使保留确认也应"一次性确认全部"而非逐份
- **用户说「不用问」之后，AI 下一次调同类 skill 又问一遍**：跨调用未持久化偏好。正确做法：当下落 memory + obs，下次直接执行
- **把确认伪装成"礼貌"**：「请问可以入库吗？」「为了稳妥再确认一下…」——这些对 AI 自己写的草稿都是冗余礼貌，浪费用户注意力

## 跨项目复用提示

**完全跨项目复用**——只要工作流是「AI 起草 → AI 自己调工具落地」就适用：

- PR 描述生成 + AI 自己 `gh pr create`：禁止再问"PR 描述确认无误？"
- commit message 草拟 + AI 自己 `git commit`（用户已授权 commit 场景）：禁止再问"commit message OK？"
- 文档生成 + 自动 `mkdocs deploy`：禁止再问"文档准备好部署？"

**核心识别准则**：
- 该确认问 = 跨主体责任移交点（user → external system 不可逆动作）→ 保留
- 该确认问 = 同主体内（AI → AI 写完自己落地）→ 删除

## 关联

- 相关 Pattern：[[PAT-14-plan-to-subagent-driven|PAT-14]]（Plan 落地默认 Subagent-Driven，同类「AI 主动执行不再问」）、[[PAT-26-ai-concise-reply|PAT-26]]（避免冗余沟通）
- Memory：`feedback_obs_inbox_to_areas_no_confirm.md`（本 Pattern 的具体落地——obs 入库 skill）
- 反向相关：[[PAT-12-no-auto-git-commit|PAT-12]]（git commit 反向——破坏性操作必须由用户主动发起，本 Pattern 不冲突，二者一起划出"AI 主动 vs 用户主动"边界）
