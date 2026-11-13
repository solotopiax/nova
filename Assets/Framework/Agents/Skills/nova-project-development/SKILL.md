---
name: nova-project-development
description: 在 Nova 消费端项目需要比较玩法方案、梳理变化中的复杂需求或安排多个依赖阶段时使用，明确关键问题、下一步与对应任务。日常共同原则由统一入口读取；明确的单点操作直接进入已有 Operation。
---

# Nova 复杂需求梳理

触发后先读取当前 Framework 的 [Docs/START_HERE.md](../../../Docs/START_HERE.md)，作为所有 `nova-project-*` Skill 的共同底线。

本 Skill 只负责需要集中梳理的复杂需求。日常原则由上述共同入口读取，完整做法维护在[方法手册](../../../Docs/Onboarding/AI_DEVELOPMENT_WORKFLOW.md)，无需先调用本 Skill 才遵循。机器分类仍为 Router，自身只读，不建立固定 Operation DAG。以下相对链接以当前已解析包内的本 Skill 真源为基准，项目投影仅供发现。

## 渐进式披露

- 先读 [contract.json](references/contract.json)，确认本次需要梳理的问题及已有事实。
- 按[阅读导航](../../../Docs/Onboarding/AI_DEVELOPMENT_WORKFLOW.md#按需阅读导航)只读当前决策分支；信息不足时再用[任务模板](references/task-templates.md)，可查事实自行补齐。
- 仅在用户分析方法依据或评估取舍时读取[来源分析](references/source-analysis.md)。
- 明确的单点小改和已有固定操作直接进入相应任务，不经过本 Skill，也不展开方法全文。

## 梳理与交接

1. 从当前项目和用户目标中提取关键未知、可比较方案及真实依赖，区分需要先实验的问题和已有事实能直接决定的问题。只补问会改变方案的业务选择。
2. 按手册的命中分支形成最小实验，或排列可独立验收的小步。每步指明目标、已有能力、必要输入和验收信号；不预先展开未选操作的全部合同，不另建长期计划账本。
3. 目标明确后直接转入已有领域 Skill 或项目源码编辑路径继续已授权工作。仅领域入口不清时使用[项目任务路由](../nova-project-router/SKILL.md)，明确目标后不再在两个 Router 间循环。各操作仍满足其原写集、确认、锁、恢复和证据门，本 Skill 不授予这些权限。

## 输出与完成边界

只要求分析或安排时，交付关键问题、取舍依据、下一步及必要输入；`success` 只表示梳理完成。要求实现时继续所选任务，最终按完整目标报告实际效果、证据、缺项和清场，不能用 Router 的静态成功代替功能交付。状态沿用 `success / partial / blocked / not_applicable`。
