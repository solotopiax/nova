---
id: ADR-036
title: 多 coder 并行强制使用 worktree 隔离
summary: ≥2 coder 强制 worktree 隔离
category: workflow
status: accepted
date: 2026-05-23
aliases:
  - ADR-036
  - ADR-036-multi-coder-mandatory-worktree
keywords: [ADR-036, ADR-036-multi-coder-mandatory-worktree, 多 coder 并行强制使用 worktree 隔离]
tags: [adr, nova, workflow, ai-collab, parallel, worktree]
supersedes: []
superseded-by: []
related:
  - "[[PAT-06-main-session-dispatch|PAT-06]]"
---

# ADR-036：多 coder 并行强制使用 worktree 隔离

## 背景（Context）

主会话调度规则一直有"两实例不得同改一文件"约束，靠主会话人工分配文件范围保证。当 ≥2 个 coder（同名多实例 或 runtime-coder + editor-coder）并行编码时：

- 同一工作区文件状态被并发写入，编译产物互污
- Unity 域重载在多实例间相互干扰，read_console 结果不可信
- 出问题时无法确定是哪个实例的改动引发
- 全靠人工纪律约束 → 容易翻车

Anthropic 官方提供的 superpowers skill `using-git-worktrees` 已经把"创建/复用 worktree、加 .gitignore、跑基线测试"做成流程化模板，社区也是这条路。

## 决策（Decision）

**≥2 个 coder 实例并行时，强制走 worktree 隔离**：

1. 主会话调用 Agent 工具时传 `isolation: "worktree"` 参数
2. 实际创建/复用流程委托 `superpowers:using-git-worktrees`，按 Step 0→1→3→4 执行
3. worktree 路径默认 `.worktrees/<branch-name>`（已写入 `.gitignore`）
4. 各 coder 在自己 worktree 内独立 `read_console` 验证编译
5. 全部返回后主会话回到主分支逐个 `git merge` → 合并后跑一次完整 `read_console` 兜底

**单 coder 单模块**：不开 worktree，原地改即可。

## 后果（Consequences）

### 正面

- 编译产物物理隔离，互不干扰
- "两实例不得同改一文件"由文件系统强约束，无需人工分配文件范围
- 出问题可在独立 worktree 内复现/调试，不污染主分支
- superpowers skill 已处理边界（已在 worktree 内的检测、submodule 守卫、.gitignore 验证），无需自己造轮子

### 负面

- 单 coder 任务开 worktree 是浪费 → 阈值定为 ≥2
- worktree 占用磁盘（Unity 项目偏大，单 worktree 可能 GB 级）→ Library/ 已在 .gitignore，但 Library 缓存仍会重建一份
- 合并阶段可能出现冲突 → 主会话需要识别并降级处理

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 继续靠"两实例不得同改一文件"人工约束 | 已多次出现并发写入冲突 |
| 自己写 worktree 创建脚本 | superpowers skill 已覆盖所有边界，无需重复实现 |
| 始终开 worktree（即使单 coder） | 磁盘开销大，单 coder 无并发问题 |
| 用 git stash 切分支模拟隔离 | 需要频繁切换，丢失编辑器状态、缓存被反复重建 |

## 验证依据（Verification）

- `.claude/agents/team-leader.md` §并行调度模板 → 模板 B
- `.gitignore` 末尾：`.worktrees/`
- `.claude/CLAUDE.md` §调度 → 模板 B 速览
- superpowers skill：`superpowers:using-git-worktrees`（Step 0 检测、Step 1b 创建、Step 4 基线验证）

## 来源（Origin）

- 会话日期：2026-05-23
- 关键对话节选：
  > 用户："具体方案的 worktree 和 mcp 的使用是否有比较科学的 skill 可以帮忙规划化流程化处理？如果有，推进！"
  > AI 决策：引入 `superpowers:using-git-worktrees`，team-leader.md 调度模板 B 写明强制触发条件

## 关联

- 规则文件：`.claude/agents/team-leader.md` §并行调度模板 → 模板 B
- 相关 Pattern：[[PAT-06-main-session-dispatch|PAT-06]]（主会话调度总纲）
- 引用的 superpowers skill：`superpowers:using-git-worktrees`
