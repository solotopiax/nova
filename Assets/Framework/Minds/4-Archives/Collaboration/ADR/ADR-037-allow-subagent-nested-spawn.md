---
id: ADR-037
title: 解除 subagent 嵌套 spawn 限制（保留主会话即调度中枢）
summary: subagent 可派下级仅作内部加速 调度仍归主会话
category: workflow
status: accepted
date: 2026-05-24
aliases:
  - ADR-037
  - ADR-037-allow-subagent-nested-spawn
tags: [adr, nova, workflow, ai-collab, dispatch, subagent]
supersedes: []
superseded-by: []
related:
  - "[[ADR-003-main-session-as-team-leader|ADR-003]]"
  - "[[ADR-034-architect-mandatory-prerequisite|ADR-034]]"
  - "[[PAT-06-main-session-dispatch|PAT-06]]"
  - "[[PAT-14-plan-to-subagent-driven|PAT-14]]"
---

# ADR-037：解除 subagent 嵌套 spawn 限制（保留主会话即调度中枢）

## 背景（Context）

ADR-003 当年的核心决策依据是"Claude Code 不支持 subagent 嵌套 spawn"，由此推导出"主会话即 team-leader"。实际上 Claude Code 工具链对 subagent 调用 `Agent()` 派下级 subagent 是开放的——这条"硬限制"叙述已经过时。

但解除 ≠ 颠覆原结论：主会话仍是用户唯一能实时双向沟通的层。原因是 Claude Code 的 `Agent` 工具是**同步阻塞**调用，subagent 一旦被 spawn，调用方整个 turn 被占用，期间不能与之对话；即便 `run_in_background: true`，也只是单向 SendMessage 喂指令，看不到实时思考流。所以：

- 主会话当 team-leader：用户随时插话、改方向、追问进度
- subagent 当二级 team-leader：用户与调度中枢之间隔了一层阻塞 subagent，反馈链断裂

历史记忆里"嵌套 spawn 失败"的说法（PAT-14、PAT-06、ADR-003、CLAUDE.md 红线）需要刷新，避免误导后续开发与跨项目复用。

## 决策（Decision）

### 一、解除"subagent 不能 spawn subagent"的限制

subagent 内部允许调用 `Agent()` 派下级 subagent。**仅作为内部加速手段**：

| 场景 | 适用 |
|---|---|
| architect 内部并行 ≥2 个 Explore 调研 codebase | ✓ |
| code-reviewer 内部并行 logic 路 + spec 路（已是模板 A） | ✓ |
| qa-reviewer 内部并行多套测试用例的执行调度 | ✓ |
| coder 内部把"自身专精的子任务"丢下级 subagent 加速 | ✓（但 scope 严格自闭） |
| subagent 自己当二级 team-leader 派 coder/reviewer/qa | ✗ |
| 需要用户实时介入 / 跨 subagent 协调的工作 | ✗（必须回到主会话调度） |

**判定原则**：subagent 派下级 subagent 用于"自我加速 / 自身能力放大"，**不是**"接管下级 agent 链"。复杂调度仍走主会话，保住用户视野。

### 二、保留"主会话即 team-leader"

这条与"嵌套限制"解耦，独立成立：

- 调度权永远在主会话——用户唯一能实时介入的层
- 主会话禁止 spawn `team-leader` agent（已内化为主会话职责，spawn 出来反而把调度委派给一个被阻塞的 subagent）

### 三、architect 不接管 coder 调度

派生决策，明确 architect 边界：

- architect 是"顾问"不是"PM"——出方案就退场，不留场调度
- 不允许"主会话只和 architect 沟通，architect 全权支配 coder/reviewer/qa"模式
- 形态分级：
  - **形态 A（日常）**：bugfix / 单模块 / Inspector 微调 → 主会话 → coder → reviewer → qa → doc，architect 不出场
  - **形态 B（中型）**：新 Component / 跨 2-3 模块 / 继承链 → architect 出方案（短脉冲一次性输出）→ 主会话拿方案派 coder
  - **形态 C（重型）**：HybridCLR / 跨程序集 / 流水线改造 → architect 深度设计 → 主会话审 → architect 拆 work package → 主会话按 work package 并行派多 coder；过程中 architect 可被回调答疑（每次新 spawn，不常驻）

### 四、连带文档刷新

- ADR-003 状态保持 `accepted`，但在 supersedes 链中标注"嵌套限制叙述被 ADR-037 部分修正"；主体决策（主会话即 team-leader）继续生效
- PAT-14 / PAT-06 反模式段落删"嵌套 spawn 失败"那条；保留"绕过主会话调度"反模式
- `.claude/CLAUDE.md` 红线："主会话尝试 spawn team-leader" 保留为独立项，去掉"嵌套 spawn 限制"括注
- `.claude/agents/team-leader.md` 现状提示改写：从"工具链不支持嵌套"改为"避免阻塞调度链；调度权留主会话"

## 后果（Consequences）

### 正面

- subagent 内部并行能力释放：architect / code-reviewer / qa-reviewer 可在自身 scope 内做并行加速
- 历史认知校正：避免新人/跨项目复用时被"不支持嵌套 spawn"的过时叙述误导
- 角色边界更清：architect 是顾问，主会话是调度，coder/reviewer/qa 是执行——三层职责清晰不重叠

### 负面

- subagent 内部 spawn 的下级活动**对用户完全不可见**（主会话只看到 subagent 最终汇总）；如果 subagent 误用嵌套 spawn 当二级调度，用户失去对项目状态感知
- 增加 subagent 实现者的判断负担：何时派下级、何时回主会话——边界靠"自我加速 vs 接管调度"语义把关
- 旧 memory / 历史 ADR 中的"嵌套限制"叙述需要长期维护刷新（不能简单删除，否则丢失决策演化轨迹）

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 维持"subagent 不能 spawn subagent"红线 | 与工具链实际能力不符；屏蔽掉合法的并行加速能力（如 code-reviewer 双路） |
| 全面解除嵌套限制，subagent 可自由派下级 agent 链 | subagent 是阻塞调用，用户失去对调度过程的实时介入；调度权外包给阻塞节点 = 反模式 |
| architect 接管所有 coder 调度，主会话只和 architect 沟通 | architect 阻塞 → 整个反馈链拉长；架构角色降级当 PM；视野倒挂；与"主会话即 team-leader"重叠 |
| team-leader agent 复活，专职调度 | 同 ADR-003：team-leader 自己是 subagent，被 spawn 后阻塞，用户失去实时通道；多一跳无价值 |

## 验证依据（Verification）

- 配置：`.claude/CLAUDE.md` 红线表更新；`.claude/agents/team-leader.md` 现状提示改写
- Pattern 同步：[[PAT-06-main-session-dispatch]] / [[PAT-14-plan-to-subagent-driven]] 反模式段落更新
- ADR 链路：本 ADR 与 ADR-003 共存，ADR-003 主体仍生效，仅"嵌套限制"叙述被本 ADR 校正
- Memory：`feedback_subagent_can_nest_spawn.md` 落 memory 索引

## 关联

- 上游：[[ADR-003-main-session-as-team-leader|ADR-003]]（主会话即调度中心，主体保留）
- 派生：[[ADR-034-architect-mandatory-prerequisite|ADR-034]]（architect 强制前置；本 ADR 进一步明确 architect 不调度 coder）
- Pattern：[[PAT-06-main-session-dispatch|PAT-06]]、[[PAT-14-plan-to-subagent-driven|PAT-14]]
- 配置：`.claude/CLAUDE.md`、`.claude/agents/team-leader.md`
