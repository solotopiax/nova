---
id: PAT-144
title: 反射调厂商私有方法是源只读约束下的最后手段，须标注版本升级复核
summary: 反射调私有厂商方法属权宜，须注释标版本复核点
category: module
type: pattern
status: active
date: 2026-07-07
source: cur-session
aliases:
  - PAT-144-reflection-private-vendor-method
keywords:
  - PAT-144
  - 厂商私有方法
  - 反射兜底
  - SDK升级复核
tags: [pattern, module, sdk, vendor, reflection]
related: []
---

# PAT-144：反射调厂商私有方法是源只读约束下的最后手段，须标注版本升级复核

## 适用场景（When）

需要触发厂商 SDK 的某个私有行为（如「清空本地缓存并用默认配置重建」），但：
- Core 源只读禁改（[[PAT-141-vendor-source-readonly|PAT-141]]），不能改成 public；
- 该行为无任何 public API 或 public 组合能达成（否则优先 [[PAT-143-vendor-sdk-missing-api-nova-layer-fill|PAT-143]]）。

## 核心做法（What & How）

在接入层用反射调私有方法，且**必须**：

1. 在 XML 注释里显式标注「反射为权宜，厂商 SDK 升级需复核方法名」。
2. 反射失败要有降级路径（`MethodInfo` 为 null 时记 Warning + 降级，不崩）。
3. 只用于触发行为，不用反射读写私有状态字段（更脆弱）。

案例：DataMaster 接入层 `ClearRuntimeCache()` 反射调私有 `ResetLocalDatabase`（drop 参数/实验表后用默认配置重建）+ 清 `PlayerPrefs DM_SEQ_CACHE` 事件序号。

## 为什么这么做（Why）

- 源只读不能破，改 Core 升级冲突。
- 反射是 public API 与组合都无解时的唯一出路，但方法名硬编码在字符串里、编译期不校验，厂商改名即静默失效——故强制注释复核点 + 降级兜底。

## 反模式（Anti-patterns）

- 反射当常规手段用（应先穷尽 public API / 组合）。
- 反射调私有却不加降级，`Invoke` 于 null 直接崩。
- 不标注版本复核点，厂商升级后无人知道这里会失效。

## 跨项目复用提示

通用于源只读第三方封装；反射永远是最后一档，前面还有 public 组合（PAT-143）、调试接口两档。

## 来源（Origin）
- 会话日期：2026-07-07
- 关键对话节选：
  > 用户：如果想模拟多设备，需要换 uid + 清理 SDK 运行缓存
  > 用户：不可以动DataMaster内部代码
  > AI：不动 Core 前提下，反射调私有 ResetLocalDatabase + 清 DM_SEQ_CACHE，注释标「版本升级需复核方法名」。

## 关联
- 相关 ADR：[[ADR-071-datamaster-topicid-is-params-key|ADR-071]]
- 相关 Pattern：[[PAT-141-vendor-source-readonly|PAT-141]]、[[PAT-143-vendor-sdk-missing-api-nova-layer-fill|PAT-143]]
