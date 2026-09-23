---
id: PAT-58
title: Pipeline 步骤失败必须显式抛错而非静默跳过
type: pattern
status: active
date: 2026-05-21
summary: Pipeline 缺失须预检并显式失败，禁止静默跳过
category: quality
aliases:
  - PAT-58
keywords:
  - PAT-58
  - Pipeline 步骤失败必须显式抛错而非静默跳过
  - Missing Step
  - 未注册 Step
tags:
  - pattern
  - methodology
  - quality
  - pipify
  - editor
---

# PAT-58：Pipeline 步骤失败必须显式抛错而非静默跳过

## 适用场景

- Pipify / 多步骤管线中，某一步依赖外部工具（如 EDM4U、HybridCLR、Luban、Protoc）的反射调用或可选 API。
- 反射查找类型 / 方法可能因为版本升级、签名变更而失败，且后续步骤依赖前序产物。
- Pipify Step 由可选 UPM Package 提供；卸载 Package、安装不完整或编译失败后，已保存 Batch 中的 StepId 暂时无法解析。

## 核心做法

1. 查不到类型 / 方法 / 调用异常时，必须抛 `InvalidOperationException`（或等价异常），由 Runner 统一捕获并标记该 Step `[FAIL]`。
2. `GetMethod` 的参数类型数组必须与目标库当前版本签名对齐；版本升级时同步校对。
3. 修改公开接口的反射签名描述 / 失败语义后，同步 XML 注释和 L2 md。
4. Runner 必须在执行任何 Step 前预检整个 Batch 的注册状态；存在 Missing Step 时一次性列出缺失 StepId 并终止，不能等执行到缺失项时才失败。
5. Window、CLI 和外部任务入口必须复用同一 Runner 门禁。Window 还应禁用运行按钮、明确显示禁用原因，并将缺失条目标为 `[Missing] StepId`。
6. Missing Step 的 `StepId + ParamsJson` 应继续保留，不得因提供方 Package 暂时不可用而自动删除；重新安装同一 Package 且 StepId 保持稳定后应自动恢复。

## 原因

- 只在循环执行到缺失项时失败，会让前序导出、构建或上传步骤先产生不可回滚的副作用。
- 自动删除缺失条目会把“暂时缺少能力”误当成“用户确认放弃配置”，破坏可恢复性。
- 只在 Window 禁用运行无法覆盖 CLI 与自动化调用，因此 Runner 必须保留最终门禁。

## 反模式

- **静默跳过**：`if (method == null) { Log.Warning("未找到方法，跳过"); return; }` —— 让管线表面绿、实际锅留给后续 Step。
- **执行到中途才发现 Missing Step**：前序 Step 已经改变配置、生成产物或上传资源，Batch 才失败。
- **自动删除未知 Step**：卸载可选 Package 时同时丢失用户已经配置的 Step 与参数，重装后无法恢复。
- **只做窗口禁用**：CLI 或外部任务仍可绕过 UI，继续启动不完整 Batch。
- **反射签名硬编码不版本对齐**：升级第三方包后不复查 `GetMethod` 的参数类型数组。
- **Console 被刷掉就当 warning 不存在**：依赖 Console 残留状态来判断成功，而不依赖产物落地校验。

## 来源与验证

- 当前实现：`EditorUtil.Pipify.Registry.GetMissingStepIds` / `GetMissingStepsReason` / `EnsureAllStepsRegistered`，以及 `PipifyWindow.DrawExecute`。
- 契约测试：`PipifyHybridClrDevelopmentBuildTests.RunBatchAsync_MissingStepFailsBeforeAnyStepExecutes` 验证缺失项位于中间时前序 Step 不执行；`GetMissingStepIds_DeduplicatesInFirstSeenOrder` 验证缺失列表稳定去重。
- 影响范围：所有由 Framework 或可选 UPM Package 注册的 Pipify Step，以及 Window、CLI、异步任务三类执行入口。
