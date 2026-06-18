---
id: PAT-06
title: 主会话调度规则
type: pattern
status: active
date: 2026-05-14
summary: 主会话调度并行派活两实例不同改一文件
category: workflow
aliases:
  - PAT-06
keywords:
  - PAT-06
  - 主会话调度规则
tags:
  - pattern
  - methodology
  - llm
  - multi-agent
  - orchestration
related:
  - "[[PAT-02-static-review-four-dim|PAT-02]]"
  - "[[PAT-03-runtime-verify-three-step|PAT-03]]"
  - "[[PAT-07-tradeoff-phased-delivery|PAT-07]]"
  - "[[ADR-006-novabehaviour-ibaselife-replace-monobehaviour|ADR-006]]"
---

# PAT-06：主会话调度规则

## 适用场景（When）

- 用 LLM 多 agent 协作（如 Claude Code subagent 体系）开发软件
- 需要明确"主会话 vs 子 agent"的边界，避免主会话越权写代码 / 子 agent 越权调度
- 团队希望把"标准通道 vs 快速通道"机器化判定，减少口头约定
- 一个任务可能涉及多模块并行，需要并行/串行编排规则

## 核心做法（What & How）

### 主会话职责

主会话 = **调度中心**，承担五件事：

1. 需求拆解
2. 任务分配
3. 进度追踪
4. 质量卡点
5. 最终交付

**主会话禁止**：直接写代码 / 写文档 / 做审查（保留 meta 配置修改权限，如 CLAUDE.md / settings.json）。

### 标准通道（默认）

```text
需求 → 主会话 → coder → code-reviewer → qa-reviewer → doc-writer → 交付
                ↑
        可选：架构变更先过 architect
```

- code-reviewer 通过才进 qa-reviewer
- qa-reviewer REJECT 打回 coder 修复后**重走** code-reviewer → qa-reviewer
- 多模块 / 新功能 / 架构变更**必走**完整流程，不适用快速通道

### 快速通道（轻量改动）

| 跳过谁 | 触发条件 |
|--------|---------|
| 跳 code-reviewer | 改动**低风险**：纯 UI 位置/大小调整、局部数值调整、文案/命名、显而易见的小 bug 修复，且**无逻辑分支新增** |
| 跳 doc-writer | 改动**不冲击对外接口**：未改 public API 签名、未增删序列化字段、未改生命周期语义、未改跨模块契约 |
| 两者叠加 | coder → qa-reviewer → 交付 |

**判定纪律**：
- 判定由主会话负责，**有任何疑虑一律走标准通道**
- 跳过 reviewer 后若 qa REJECT，打回 coder 修复后**必须补走** reviewer

### 并行规则

- **能并行必须并行**：独立叶子类型（数据结构 / 接口 / 异常）全部并行发车
- 串行卡点：接口就绪 → Manager/Component；代码落地 → reviewer → qa → doc-writer
- **同角色并行实例**：单角色同时不超过 8 实例

> [!danger] 隔离铁律
> 两实例绝不同改一文件——违反即数据竞争，后写的覆盖先写的，无 merge 工具调和。

### 通讯协议

```text
[交接] from → to | 任务 | 变更文件 | 状态(完成/部分/需修复) | 备注
[审查] from → to | P0(x) P1(x) P2(x) P3(x) P4(x) | 列表
[交付] 主会话 | 任务 | code-reviewer:[CHECK-PASS / 跳过-原因]
                   | qa-reviewer:[PASS]
                   | doc:[完成 / 跳过-原因]
                   | 文件清单
```

## 为什么这么做（Why）

- **主会话不写代码**：调度者一旦下场写代码，就失去对全局的视野；同时上下文被业务细节污染，质量卡点失效
- **标准通道默认 + 快速通道例外**：默认全流程保证下限，例外通过明确条件控制风险
- **跳过条件机器化**：用"是否改 public API / 是否新增逻辑分支"这种可机械判定的条件，避免"我觉得这个改动小"
- **REJECT 必须重走 reviewer**：快速通道跳过的环节在出问题后必须补——这是承担"快速"风险的对等代价
- **并行 ≤8 实例**：超过这个数主会话调度负担超过收益（要追踪每个实例状态、合并冲突）
- **同改一文件 = 数据竞争**：两 agent 同时改一文件 = 后写的覆盖先写的，且没有 merge 工具调和——这条违反必出事故
- **通讯协议结构化**：让交接、审查、交付的状态可被解析、可对账，事后能复盘"哪一步出了什么问题"

## 反模式（Anti-patterns）

- **主会话越权写代码**：图省事自己改两行，导致质量防线崩塌、上下文污染、协议失效
- **快速通道滥用**：把"新增逻辑分支"硬说成"小修复"，跳过 reviewer，bug 在 qa 才被发现
- **跳过后 REJECT 不补审查**：qa 打回，coder 直接修了再跑 qa，跳过的 reviewer 永远没补
- **多 agent 同改一文件**：以为"反正最后会 merge"，实际后写覆盖先写，丢工作
- **不并行**：明明 5 个独立类型可以并行，主会话串行调度，把 1 小时拖成 5 小时
- **架构变更走快速通道**：以为只是改一个 Manager，实际牵动继承链，跳过 reviewer 之后引发连锁 bug
- **交付时省略协议字段**：不写"跳过原因"，事后无法追溯"为什么这次跳了 doc-writer"
- **主会话 spawn `team-leader`**：技术上虽允许嵌套 spawn（[[ADR-037-allow-subagent-nested-spawn|ADR-037]]），但 team-leader 已内化为主会话职责；spawn 出来等于把调度权托管给阻塞 subagent，用户失去实时介入能力
- **subagent 内部 spawn 当二级 team-leader**：嵌套 spawn 仅供 subagent "自我加速"（如 architect 内部并行 Explore），不得用作"调度下级 agent 链"
- **architect 接管 coder/reviewer/qa 调度**：architect 是顾问不是 PM，出方案就退场；让 architect 留场调度 = 主会话失去全局视野 + 反馈链拉长

## 跨项目复用提示

- **思想可迁移**：任何 multi-agent / 团队协作场景都适用——主调度 + 专职执行 + 质量门 + 文档收尾
- 角色映射按团队改：
  - 人类团队：主会话 = PM/技术负责人；coder = 开发；code-reviewer = 同行评审；qa-reviewer = QA；doc-writer = 文档/产品
  - LLM 团队：直接照搬本规则
  - 混合团队：人类做调度 + LLM 做执行
- 快速通道条件按项目敏感度调整：
  - 金融 / 医疗：禁用快速通道，全部走标准
  - 内部工具 / 探索原型：可放宽到"非破坏性 UI 改动"也能跳 reviewer
- 并行上限按工具能力调：人脑/IDE 能管 3~5，LLM 能管 8，CI 自动化能管几十
- **不可省略的核心**：①调度者不下场 ②质量门顺序 ③隔离铁律 ④通讯协议结构化
- 不适合：单人项目（无需调度）/ 一次性脚本

## 关联

- 主源：`.claude/CLAUDE.md` Team 编制与流程节
- Agent：`.claude/agents/team-leader.md`（保留为职责说明书）
- 配套：[[PAT-02-static-review-four-dim|PAT-02]] 静态审查 / [[PAT-03-runtime-verify-three-step|PAT-03]] 运行时验证 / [[PAT-07-tradeoff-phased-delivery|PAT-07]] 分阶段交付
