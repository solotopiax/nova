---
id: PAT-58
title: Pipeline 步骤失败必须显式抛错而非静默跳过
type: pattern
status: active
date: 2026-05-21
summary: 反射/外部 API 失败必抛异常禁静默跳过
category: quality
aliases:
  - PAT-58
keywords:
  - PAT-58
  - Pipeline 步骤失败必须显式抛错而非静默跳过
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

## 核心做法

1. 查不到类型 / 方法 / 调用异常时，必须抛 `InvalidOperationException`（或等价异常），由 Runner 统一捕获并标记该 Step `[FAIL]`。
2. `GetMethod` 的参数类型数组必须与目标库当前版本签名对齐；版本升级时同步校对。
3. 修改公开接口的反射签名描述 / 失败语义后，同步 XML 注释和 L2 md。

## 反模式

- **静默跳过**：`if (method == null) { Log.Warning("未找到方法，跳过"); return; }` —— 让管线表面绿、实际锅留给后续 Step。
- **反射签名硬编码不版本对齐**：升级第三方包后不复查 `GetMethod` 的参数类型数组。
- **Console 被刷掉就当 warning 不存在**：依赖 Console 残留状态来判断成功，而不依赖产物落地校验。
