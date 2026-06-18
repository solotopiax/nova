---
id: ADR-034
title: architect 五种强制前置触发条件
summary: architect 五条强制前置触发条件命中即派
category: ai-collab
status: accepted
date: 2026-05-23
aliases:
  - ADR-034
  - ADR-034-architect-mandatory-prerequisite
keywords: [ADR-034, ADR-034-architect-mandatory-prerequisite, architect 五种强制前置触发条件]
tags: [adr, nova, ai-collab, workflow, architecture]
supersedes: []
superseded-by: []
related:
  - "[[ADR-001-component-manager-three-layer|ADR-001]]"
  - "[[ADR-007-procedure-tier-split|ADR-007]]"
  - "[[PAT-06-main-session-dispatch|PAT-06]]"
---

# ADR-034：architect 五种强制前置触发条件

## 背景（Context）

主会话调度规则原本把 architect 标为"可选——仅新模块/继承体系/跨模块依赖"。实际操作中：

- "新模块"边界模糊，主会话经常自行判断"小改动不必 architect"，直接派 coder
- 跨模块改动事后才暴露架构问题，返工成本远高于事前 architect 出方案
- "用户确认环节"卡在 architect 出方案后再让用户审阅，牺牲流畅性，多数情况用户最终都同意

需要明确触发列表 + 决策权归属，避免主会话每次都"自己判断"。

## 决策（Decision）

任一条件命中**必须**先派 architect，方案出来后**主会话直接派 coder**（无需用户确认环节）：

1. 新建 Component / Manager / Procedure
2. 修改三层 Manager 继承链（Interface / ManagerBase / Manager 任一层）
3. 跨 ≥2 模块的接口变更
4. 新增 partial class 文件 ≥3 个
5. 涉及 HybridCLR / 业务 DLL / Procedure 注册顺序

**不触发条件（直接派 coder）**：

- 修 bug、改实现细节
- 改 UI 文案 / 局部数值 / 命名
- 局部逻辑分支调整
- 单文件 Inspector 字段增删（不涉及序列化结构变迁）

跳过 architect 直接派 coder = 红线违规（写入 CLAUDE.md 禁止行为节）。

## 后果（Consequences）

### 正面

- 架构敏感改动有明确触发条件，不再依赖主会话自由心证
- 重大架构变更早期发现成本远低于事后返工
- 取消用户确认环节，流畅性显著提升
- 调度路径可审计：触发条件命中即可 grep 出对应 architect 调用

### 负面

- 用户对 architect 方案的最终把关被前置到「用户审最终交付」环节，而非「架构方案阶段」
- 触发条件需要保持精确——条件过宽则 architect 被滥用，过窄则架构改动绕过
- "新增 partial class 文件 ≥3 个"是经验值，可能与"新建 Component"重叠，但保留作冗余兜底

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 保留"用户确认 architect 方案"环节 | 牺牲流畅性，多数情况用户最终同意 |
| 让 coder 自行判断是否需要 architect | coder 视角偏实现细节，对全局架构敏感性低 |
| 不强制前置，事后 reviewer 把关 | 事后发现的架构问题返工成本远高于前置 |
| 触发列表更宽（如所有跨文件改动） | architect 滥用，简单改动也卡 architect |

## 验证依据（Verification）

- `.claude/agents/team-leader.md` §并行调度模板 → 模板 C
- `.claude/CLAUDE.md` §红线表新增「模板 C 触发命中跳过 architect」违规项

## 来源（Origin）

- 会话日期：2026-05-23
- 关键对话节选：
  > AI 优化建议："architect 强制前置（复杂改动）—— 减少返工，方案先行"
  > 用户："2.保留，且可以直接推进，不需要确认"（即保留触发条件硬触发，但取消用户确认环节）

## 关联

- 规则文件：`.claude/agents/team-leader.md` §并行调度模板 → 模板 C
- 相关 ADR：[[ADR-001-component-manager-three-layer|ADR-001]]（三层 Manager 继承链）
- 相关 ADR：[[ADR-007-procedure-tier-split|ADR-007]]（Procedure 分档）
- 相关 Pattern：[[PAT-06-main-session-dispatch|PAT-06]]（主会话调度总纲）
