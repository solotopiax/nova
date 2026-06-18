---
id: PAT-131
title: 派 Demo UI 子 agent 必须复述精确视觉规范参数
summary: 派 UI 子 agent 必复述精确视觉参数
category: ai-collab
type: pattern
status: active
date: 2026-06-01
aliases:
  - PAT-131-dispatch-restate-exact-ui-specs
tags: [pattern, ai-collab, dispatch, demo, ui, prefab]
related:
  - "[[PAT-105-api-hint-near-element-split|PAT-105]]"
  - "[[PAT-80-demo-view-pure-color-style|PAT-80]]"
  - "[[PAT-79-demo-view-portrait-layout|PAT-79]]"
  - "[[PAT-102-button-overlay-sub-hint-layout|PAT-102]]"
---

# PAT-131：派 Demo UI 子 agent 必须复述精确视觉规范参数

## 适用场景（When）

- 主会话把 Demo View / prefab 制作或修改任务派给子 agent（editor-coder 等）。
- 任务涉及已有精确视觉规范（色值、控件高度、字号、布局参数、组件勾选项）的元素。
- 任何"规范已沉淀在 Vault，但执行交给不主动逐条查 Vault 的子 agent"的场景。

## 核心做法（What & How）

派活 prompt 中必须**复述精确数值**，而非泛词。对照表：

| ❌ 泛词（禁） | ✅ 精确复述（必须） |
|---|---|
| "按钮 API 提示用蓝色" | "ApiHintText color = 深蓝 #1A3A8C = RGBA(0.10196,0.22745,0.54902,1)，FontSize 18，贴底居中（PAT-105/102）" |
| "按钮做大一点" | "控件高度 ≥80px，白底黑字 fontSize=28 不换行居中（PAT-80）" |
| "竖屏布局" | "768×1666，match-by-width=0，TitleBar 120px，InteractionArea anchor(0,0.4)~(1,1)（PAT-79）" |
| "动态行容器配好 VLG" | "VLG ChildControlHeight 按行类型决定（动态 TMP 子行=true / 固定高按钮行=false），见 PAT-103" |

执行流程：派 Demo UI 活前，主会话先 grep 命中相关 PAT（105/102/80/79/103/77），把精确数值表抄进子 agent prompt；子 agent 的职责是"按表执行"，不是"重新设计视觉"。

## 为什么这么做（Why）

- **子 agent 不会主动逐条核 Vault 精确色值**：给泛词它就用经验默认值。真实案例：DemoLoginView 的 ApiHintText 因 prompt 只写"蓝色"，子 agent 用了浅蓝 `(0.3,0.6,1)`，违反 PAT-105 的深蓝 `#1A3A8C`，被用户打回返工。
- **规范已存在 ≠ 会被遵守**：Vault 沉淀的是"应该这样"，但执行链路里若主会话不复述，规范在子 agent 那里等于不存在。
- **复述成本 << 返工成本**：抄一张数值表几行字，省掉一轮 prefab 重做 + qa 复验。

## 反模式（Anti-patterns）

- **派活 prompt 用"蓝色/大按钮/竖屏"泛词**：子 agent 落地经验值，偏离精确规范，下一轮被打回。
- **假设子 agent 会自己查 PAT-105 拿色值**：子 agent 通常只做 Vault 前置匹配的"命中列举"，不会逐条把色值落到操作里，除非 prompt 明确给值。
- **主会话自己也没查精确值就派**：泛词派活=把"查规范"的责任甩给不会查的下游，规范彻底失守。
- **只在出错后才补色值**：第一次就该给全，事后补=已经返工。

## 跨项目复用提示

- "委托执行时把隐性规范显性化为精确参数表"适用于任何"规范库 + 执行代理"分离的协作结构（含人类外包、CI 模板、其他 AI agent）。
- 视觉规范尤其需要——色值/尺寸的"差不多"在像素层面就是"错"，没有模糊容忍区间。

## 关联

- 规则源：候选补充至 `.claude/agents/team-leader.md`「派活 prompt 复述精确规范」条
- 相关 Pattern：[[PAT-105-api-hint-near-element-split|PAT-105]]（深蓝色值权威源）、[[PAT-80-demo-view-pure-color-style|PAT-80]]（纯色块样式）、[[PAT-79-demo-view-portrait-layout|PAT-79]]（竖屏参数）、[[PAT-102-button-overlay-sub-hint-layout|PAT-102]]（按钮副提示布局）
- 相关 memory：[[feedback_dispatch_restate_exact_ui_specs]]
