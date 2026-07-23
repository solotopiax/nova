---
id: PAT-147
title: UPM 发版前禁止默认批量运行 Test Runner
summary: UPM发版禁默认批量Test Runner
category: workflow
type: pattern
status: active
date: 2026-07-21
source: cur-session
aliases:
  - PAT-147-upm-publish-no-bulk-test-runner
keywords:
  - UPM 发版测试边界
  - 批量 Test Runner
  - 发布前验证预算
tags: [pattern, methodology, upm, publishing, testing, efficiency]
related:
  - PAT-03
  - PAT-113
---

# PAT-147：UPM 发版前禁止默认批量运行 Test Runner

## 适用场景（When）

- 用户要求发布一个或多个 Nova UPM 包。
- 发布前已经完成版本撞号、依赖闭环、发布资料、签名环境、编译或 dry-run 等必要预检。
- 仓库存在大量 EditMode / PlayMode 测试，但本轮改动只涉及有限模块或发布资料。

## 核心做法（What & How）

### 1. “发版”不隐含批量测试授权

UPM 发版的默认验证范围是：

- 目标包与版本合法性
- registry 撞号检查
- 发布资料与依赖闭环
- 签名打包 dry-run
- 发布后的版本可见性、`dist-tags.latest` 与临时态还原

不得把 Unity Test Runner 全量 EditMode、全量 PlayMode或跨模块批量测试自动追加为发布门禁。

### 2. 只执行与风险直接相关的定向验证

需要补充测试时，必须同时满足：

1. 测试能直接覆盖本轮改动的具体风险，而不是泛化的“多跑一些更保险”。
2. 测试范围可明确到目标模块、fixture 或 case。
3. 用户已明确要求测试，或已确认该定向测试会占用的额外时间。

不满足时，继续既定发布链路，不得擅自扩大验证范围。

### 3. 用户要求停止测试后立即收口

用户明确表示“直接发版”“不要跑 Test”或指出测试浪费时间后：

- 停止尚未开始的测试任务。
- 不再追加其他 Test Runner 验证。
- 只继续签名、发布和 registry 发布后校验。
- 最终如实说明哪些测试未执行，不把未执行包装成已通过。

### 4. 与运行时验证规范的边界

[[PAT-03-runtime-verify-three-step|PAT-03]] 用于需要验证代码运行行为的开发任务；它不代表每次 UPM 发版都必须重跑整个仓库的行为测试。发版任务应复用本轮已有的有效验证证据，并按变更风险决定是否需要额外的定向测试。

## 为什么这么做（Why）

- 多包仓库的批量 Test Runner 成本高，可能显著延迟本来只需完成签名和 registry 校验的发布任务。
- 与改动无关的全量测试不会等比例提高本次发布结论的可信度。
- 未经确认扩大测试范围，会打破用户对发版耗时和交付节奏的预期。
- 发布验证和代码行为测试是两类门禁：前者证明包可正确发布和安装，后者证明具体行为满足预期，不能机械捆绑。

## 反模式（Anti-patterns）

- 用户只要求发版，却默认启动全量 EditMode 与 PlayMode 测试。
- 已完成编译和 dry-run 后，再以“更稳妥”为由追加跨模块批量测试。
- 用户要求停止测试后，换一个测试入口继续执行。
- 用“测试通过”替代签名产物、registry 可见性或 `dist-tags.latest` 校验。
- 完全跳过与本轮高风险代码直接相关且已经明确授权的定向测试。

## 验证依据（2026-07-21）

一次 13 包 UPM 发布中，额外启动 Test Runner 明显增加了等待时间，且不属于用户要求的发布范围。停止批量测试后，既定发布链路完成了 13 个包的签名与发布，结果为 `Published: 13, Failed: 0`；随后逐包验证目标版本与 `dist-tags.latest`，并确认 Sample 临时目录和 `Nova.prefab` 已还原。

## 关联

- 代码运行时验证：[[PAT-03-runtime-verify-three-step|PAT-03]]
- UPM 版本事实源：[[PAT-113-no-manual-version-bump|PAT-113]]
- 发布流程：`.agents/skills/nova-publish/SKILL.md`
