---
id: ARC-01
title: 废弃 SDK Diagnostics 中间层（观测下沉至 Log 层）
status: recorded
archive-mode: direct
needs-review: false
date: 2026-05-18
deprecated-on: 2026-04-28
archived-date: 2026-05-18
source: docs-migration-from-sdk-architecture
aliases:
  - sdk-diagnostics-deprecated
tags:
  - archive
  - adr-deprecated
  - nova
  - framework
  - sdk
related:
  - "[[ADR-022-sdk-plugin-architecture]]"
---

# ARC-01：废弃 SDK Diagnostics 中间层

## 原决策（已废弃）

SDK 模块 S 级重构早期版本曾设计独立诊断层 `ISDKDiagnostics` / `SDKPluginDiagnostic` / `SDKDiagnosticsImpl`，对外暴露每个 Plugin 的初始化状态、耗时与异常信息，供 Inspector Runtime 面板与外部监控消费。

涉及类型：

- `ISDKDiagnostics`（接口）
- `SDKPluginDiagnostic`（数据结构）
- `SDKDiagnosticsImpl`（SDKManager 内嵌类）
- `ISDKManager.Diagnostics` 属性
- `SDKManagerBase.Diagnostics` abstract 声明
- `SDKComponent.Diagnostics` 属性

## 废弃日期与原因

- **废弃日期**：2026-04-28
- **用户决策**：观测需求由 `Log` 层承担，删除 Diagnostics 中间层以简化架构
- **核心理由**：
  1. Diagnostics 引入了独立数据结构（`SDKPluginDiagnostic`）和接口层（`ISDKDiagnostics`），实际观测需求 `Log.Error` / `Log.Warning` / `Log.Info` 已能完全覆盖
  2. Inspector 面板 Runtime 展示需求当前不在落地范围内，无需提前设计
  3. 维护多一层数据聚合 = 多一份易过期的事实源

## 替代方案

失败/状态观测节点统一收敛到 Log：

| 场景 | Log 级别 | 关键字 |
|---|---|---|
| Plugin 初始化失败（异常） | `Log.Error` | Plugin 名 + 异常摘要 |
| Plugin 类型 Missing（UPM 卸载） | `Log.Warning` | TypeLoadFailure: <TypeName> |
| `SetConfig` 在 `InitializeAsync` 之后调用 | `Log.Warning` | 忽略本次调用 |
| Plugin 初始化成功 | `Log.Info` | Plugin 名 + Duration |

业务侧若需运行时探测可用性，使用：
- `Nova.SDK.TryGet<T>(out var plugin)` 返回 false 即不可用
- 单 Plugin 的 `IsAvailable` 标志位（接口 `ISDKPlugin.IsAvailable`）

## 处置结果（2026-05-18）

- [x] **direct 归档**：直接进 `4-Archives/Records/`（无前置正式 ADR，本就是设计阶段废弃的草案）

## 影响清单

- 已删除文件：`ISDKDiagnostics.cs`、`SDKPluginDiagnostic.cs`、`SDKDiagnosticsImpl`（内嵌于 SDKManager）
- 已移除属性：`ISDKManager.Diagnostics`、`SDKManagerBase.Diagnostics`、`SDKComponent.Diagnostics`
- 文档清理：原 SDK/ARCHITECTURE.md §18 ADR-007 段落、§7 时序图中 Diagnostics 引用

## 来源

- 文档：`Assets/Framework/Docs/Runtime/Modules/SDK/ARCHITECTURE.md` §18 ADR-007（L1151-1157）
- 用户决策：2026-04-28 主会话拍板废弃

---
*归档时间 2026-05-18，处置方式：direct 归档（无前置正式 ADR）。*
