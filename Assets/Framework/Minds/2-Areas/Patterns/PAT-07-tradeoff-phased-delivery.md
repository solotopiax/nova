---
id: PAT-07
title: Trade-Off 分析框架 + 分阶段交付
type: pattern
status: active
date: 2026-05-14
summary: Trade-Off方案分阶段交付小步验证
category: workflow
aliases:
  - PAT-07
keywords:
  - PAT-07
  - Trade-Off 分析框架 + 分阶段交付
tags:
  - pattern
  - methodology
  - architecture
  - decision
related:
  - "[[PAT-06-main-session-dispatch|PAT-06]]"
  - "[[PAT-08-architecture-antipatterns|PAT-08]]"
  - "[[ADR-007-procedure-tier-split|ADR-007]]"
---

# PAT-07：Trade-Off 分析框架 + 分阶段交付

## 适用场景（When）

- 架构师 / 技术负责人在做不可逆的设计决策（继承体系、跨模块依赖、技术栈选型）
- 决策需要事后被复审、被理解、被反驳，不能只留下"当时这么定的"
- 大型变更无法一次性完成，需要拆分为可独立合入的阶段
- 团队希望避免"全部完成才能用"——风险集中爆发，没有中间产出

## 核心做法（What & How）

### Trade-Off 四件套

每个设计决策必须文档化以下四项，缺一不可：

| 字段 | 内容 |
|------|------|
| **Pros** | 这个方案的收益（性能、可维护性、扩展性、上手成本） |
| **Cons** | 这个方案的代价与局限（技术债、复杂度、对未来选择的限制） |
| **Alternatives** | 被排除的方案 + **为什么不选**（关键，避免重复讨论） |
| **Decision** | 最终选择 + 理由（决定权归属 + 决策时间） |

输出格式参考 ADR（Architecture Decision Record）：

```markdown
# ADR-XXX: [决策标题]

## 背景
为什么需要这个决策（驱动力 / 约束 / 触发事件）

## 决策
选择了什么方案（结论 + 关键约束）

## 后果
正面影响 + 负面影响 + 需要持续监控的风险

## 被排除的方案
- 方案 A：被排除原因
- 方案 B：被排除原因

## 状态
[Proposed / Accepted / Deprecated / Superseded by ADR-YYY]
```

### 分阶段交付

大型变更拆分为**可独立合入**的阶段，每阶段独立走完审查 + 验证流程：

| Phase | 目标 |
|-------|------|
| **Phase 1：最小可用** | 最小切片提供价值；可能只覆盖 happy path 主线 |
| **Phase 2：核心体验** | 完整 happy path；常用场景全覆盖 |
| **Phase 3：边界处理** | 错误路径、边界条件、异常输入 |
| **Phase 4：优化** | 性能、监控、可观测性、tooling |

**铁律**：
- 每个 Phase 必须**可独立合入**——禁止"全部完成才能用"的方案
- 每个 Phase 有明确的验收标准和文件范围
- 每个 Phase 独立走标准通道（[[PAT-06-main-session-dispatch|PAT-06]]）：coder → reviewer → qa → doc-writer
- 优先交付最小可用切片；老板不满意 Phase 1，及时调整 Phase 2 不浪费

## 为什么这么做（Why）

- **Trade-Off 四件套防止"凭直觉决策"**：写下 Cons 强迫面对代价；写下 Alternatives 防止半年后重新讨论同一问题
- **Decision 字段定责任人**：避免"当时大家都同意了"这种集体无责
- **状态标签支持决策演化**：技术栈会变，旧 ADR 标记 Deprecated 不删除，留作历史
- **分阶段消除"全有或全无"风险**：Phase 1 上线即可拿用户反馈，方向错了立即调整，比 Phase 4 完工后才发现要改省 80% 成本
- **可独立合入降低 merge 难度**：每个 Phase 都是完整 PR，不会出现"分支拉了三个月与主干完全冲突"
- **每 Phase 走完整流程 = 持续质量保证**：避免最后阶段才发现 Phase 1 的设计有缺陷
- **优先级显式**：Phase 1 = 最小可用，强迫团队回答"什么是真的必须有"，砍掉镀金需求

## 反模式（Anti-patterns）

- **决策只写结论**：只说"我们选了 A 方案"，半年后没人记得为什么不选 B，每次新人来都要重新讨论
- **Cons 留白**：只列收益不列代价，决策评审看起来无懈可击，落地后才发现技术债
- **Alternatives 假装没考虑过**：跳过这一段，后人怀疑"你是真比较过还是拍脑袋"
- **大爆炸交付**：所有 Phase 合一个分支，3 个月后才合入，merge 冲突 + 设计偏差全在最后爆炸
- **Phase 1 镀金**：把 Phase 1 做到 80% 完整度，失去"最小可用"的快速反馈意义
- **Phase 不可独立**：Phase 2 需要 Phase 1 + Phase 3 都到位才能跑——本质上还是大爆炸
- **跳过 reviewer 凑进度**：每个 Phase 应独立走质量门，不能"前期糙后期补"——后期永远不补
- **ADR 写完不维护**：决策被推翻后旧 ADR 还标 Accepted，新人按废弃方案设计

## 跨项目复用提示

- **完全可迁移**：Trade-Off + ADR + 分阶段是所有领域的通用方法（不限软件，制造/产品/组织变革都适用）
- ADR 模板在各社区有成熟变体（adr-tools / MADR / Y-statement），按团队偏好选
- Phase 划分按项目类型：
  - 后端服务：MVP API → 完整业务 → 错误处理 → 性能/监控
  - 前端：UI 骨架 → 主交互 → 边缘 case → 性能/可访问性
  - 数据管线：Happy path 一条链路 → 全场景 → 错误重试 → 监控告警
  - 嵌入式：核心驱动 → 完整功能 → 鲁棒性 → 功耗优化
- ADR 持久化位置：源码仓库内 `docs/adr/` 或独立知识库（如 Obsidian Vault）
- **配套必备**：质量门体系（[[PAT-02-static-review-four-dim|PAT-02]] / [[PAT-03-runtime-verify-three-step|PAT-03]]）保证每 Phase 真的能合入
- 不适合：研究性项目 / 一次性脚本（决策频繁回滚，ADR 维护成本高于收益）

## 关联

- 配套：[[PAT-06-main-session-dispatch|PAT-06]] 主会话调度（每 Phase 走完整通道）/ [[PAT-08-architecture-antipatterns|PAT-08]] 架构反模式（Trade-Off 帮助识别）
