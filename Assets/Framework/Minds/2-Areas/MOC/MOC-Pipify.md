---
id: MOC-Pipify
title: Pipify 构建流水线图谱
summary: 导出、HybridCLR、Bundle 构建的 Editor 流程入口速查
category: editor
status: active
date: 2026-06-05
aliases:
  - MOC-Pipify
  - Pipify图谱
  - 构建流水线图谱
tags: [moc, nova, pipify, editor, hybridclr, yooasset]
keywords: [Pipify, Runner, PipifyStep, DataPipeline, BundleBuilder, HybridCLR, Export, BuildProcessor]
related:
  - "[[ADR-026-pipify-runner-no-batch-locking|ADR-026]]"
  - "[[ADR-027-rule-ban-editor-refcount-batch-apis|ADR-027]]"
  - "[[ADR-028-hybridclr-copy-aot-after-buildplayer|ADR-028]]"
  - "[[ADR-031-upm-three-piece-mandatory|ADR-031]]"
  - "[[PAT-49-pipify-step-no-batch-lock-assumption|PAT-49]]"
  - "[[PAT-51-sbp-buildcache-vs-lock-reload-incompat|PAT-51]]"
  - "[[PAT-58-pipeline-fail-fast-no-silent-skip|PAT-58]]"
---

# MOC-Pipify：Pipify 构建流水线图谱

## 一句话

Pipify 是 Nova 的 Editor 批处理入口，用来串起导出、HybridCLR 产物准备、Bundle 构建等步骤；它负责调度，不负责替 Step 持有任何“全局批锁状态”。

## 何时查这页

- 要新增或修改 `PipifyStep`
- 要调整 Excel 导出、HybridCLR、Bundle 构建顺序
- 要判断某个构建动作属于 Runner、Step，还是邻接工作流

## 主入口

```text
PipifyWindow / CLI
  -> EditorUtil.Pipify
  -> Runner.RunBatchAsync(...)
  -> Registry.FindById(...)
  -> PipifySteps.*
```

## 这页只记的 4 条主线

| 主线 | 入口 | 作用 |
|---|---|---|
| 导出 | `PipifySteps.Export.*` | 把表格/配置源转换成运行时产物 |
| HybridCLR | `PipifySteps.HybridCLR.cs` | 生成并拷贝 AOT metadata / 业务 DLL 资产 |
| Bundle | `PipifySteps.BundleBuilder.cs` | 触发 YooAsset 资源构建 |
| 构建期补充 | `PipifySteps.Build.cs` / `BuildProcessor/` | App Build 与构建期规则注入 |

## 边界

- 按 `BatchItem` 顺序执行
- 通过 `Registry` 定位 Step
- 做参数覆盖、进度汇报、失败即停
- 不做 `StartAssetEditing`
- 不做 `LockReloadAssemblies`
- 不替 Step 维护“上一阶段遗留状态”
- 不把发布、构建、导出混成一个隐式大事务
- 自己完成本步骤所需前置条件
- 需要 importer 立即生效时，自己显式 `ImportAsset(..., ForceSynchronousImport)`
- 失败即抛出，不静默跳过

## 与其他系统的关系

- `DataPipeline/`：承载具体导出实现；Pipify 负责调度这些实现，不替代它们。
- `BuildProcessor/`：承载 Build 期规则和平台处理；Pipify 只是把它串进流程。
- `HybridCLR`：其核心约束是“`BuildPlayer` 之后拷 AOT，`BundleBuilder` 之前完成 DLL 产物准备”。
- `UPM 发布`：是 Pipify 邻接流程，不是 Runner 本体。三件套和版本节约束以 `ADR-031` 为准，这页不展开具体发布工具。

## 常见误区

- 以为 Runner 会替所有 Step 持有批锁或刷新状态
- 把导出实现写进 Runner，而不是放回 `PipifySteps.*` / `DataPipeline`
- 把“发布规则”写进模块图谱，导致 MOC 变成操作手册
- 为了图省事在 Step 里静默吞错，破坏 fail-fast

## 先往哪看

- 改 Runner 语义：[[ADR-026-pipify-runner-no-batch-locking]]
- 改批锁 / 刷新相关假设：[[ADR-027-rule-ban-editor-refcount-batch-apis]]、[[PAT-49-pipify-step-no-batch-lock-assumption]]
- 改 HybridCLR 构建顺序：[[ADR-028-hybridclr-copy-aot-after-buildplayer]]
- 改失败策略：[[PAT-58-pipeline-fail-fast-no-silent-skip]]

## 关联

- 图谱：[[MOC-HybridCLR]]、[[MOC-Config]]
