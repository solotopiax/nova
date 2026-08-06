---
id: PAT-151
title: Demo API Hint 源码-Prefab 闭环门禁
summary: 全局门禁闭环 Demo API 提示源码与 Prefab
category: demo
type: pattern
status: active
date: 2026-08-06
aliases:
  - PAT-151-demo-api-hint-source-prefab-gate
keywords:
  - PAT-151
  - Demo API Hint 源码-Prefab 闭环门禁
  - SetButtonApiHint
  - SetFieldApiHint
  - ApiHintText
  - Demo API 提示回归
tags: [pattern, demo, ui, api-hint, prefab, testing, regression]
related:
  - "[[ADR-039-base-demo-view-api-hint-split|ADR-039]]"
  - "[[PAT-105-api-hint-near-element-split|PAT-105]]"
  - "[[PAT-66-no-handcraft-prefab|PAT-66]]"
  - "[[PAT-78-sample-demo-full-flow-sop|PAT-78]]"
---

# PAT-151：Demo API Hint 源码-Prefab 闭环门禁

## 适用场景

- 新增或修改 Nova Sample Demo 的按钮、字段或调用接口。
- 使用 `SetButtonApiHint`、`SetFieldApiHint` 或封装方法设置 API 提示。
- 通过模板动态克隆按钮，并在运行时为克隆对象设置 API 提示。
- 对 Demo UI 做全局迁移、补齐或回归检查。

## 硬规则

1. 每个 API 提示调用都必须传入非空的调用代码文本，不能用 SKU 等业务展示数据代替。
2. 直接字段接收者必须在对应 View Prefab 中存在并绑定组件。
3. 接收者必须拥有直属 `ApiHintText`，该节点必须包含 `TMP_Text` 实现组件。
4. 包装方法调用必须展开到实际序列化字段检查，不能只检查包装方法内部的一次 `SetButtonApiHint`。
5. 动态克隆按钮必须检查克隆模板结构，并检查运行时传入的最终提示文本。
6. Prefab 结构修改必须遵守 [[PAT-66-no-handcraft-prefab|PAT-66]]，通过 Unity 正常序列化和保存。

## 核心做法

维护一个全局 Editor 契约测试，扫描 `Assets/Samples` 下的直接提示调用与项目约定的包装调用，并建立以下闭环：

```text
源码提示调用
  -> View 序列化字段
  -> Prefab 组件引用
  -> 直属 ApiHintText
  -> TMP_Text
  -> 非空且表达实际调用的代码文本
```

新增或修改任何 Demo 交互后，必须先运行全局契约测试，再运行当前模块的专项回归测试。局部专项测试不能替代全局门禁。

## 为什么需要门禁

`SetButtonApiHint` 和 `SetFieldApiHint` 在找不到 `ApiHintText` 时会静默返回。源码里存在调用不代表 UI 一定显示，编译通过也不会暴露 Prefab 节点缺失。只有把源码接收者、序列化引用、Prefab 节点、TMP 组件和提示文本同时验证，才能证明“代码接口文本实际可显示”。

## 本次遗漏的自我反省

[[PAT-105-api-hint-near-element-split|PAT-105]] 已经明确规定工具方法不会在运行时创建节点，Prefab 必须预置 `ApiHintText`。本次问题不是规则不清楚，而是执行和验证没有落实：

- 只关注了新增 Query 按钮的调用代码和专项测试，没有把检查范围扩展到所有 Demo。
- 把“源码中调用了 `SetButtonApiHint`”误当成“UI 已显示提示”，忽略了缺节点时静默返回的语义。
- 没有逐一建立调用点与 Prefab 序列化字段的映射，因此未发现 34 个既有按钮缺少直属节点。
- 没有检查提示参数的语义，因而遗漏 IAP 使用空串和 SKU 代替调用代码的两处问题。
- 已有 PAT-105 只停留在人工规范，没有及时转化为自动回归门禁。

责任在于实施收口不完整，而不在既有规则。以后不能再以局部功能通过、源码存在或编译通过作为 Demo UI 完成的依据。

## 防复发检查

- 改动 Demo 按钮或字段前后都运行全局 API Hint 契约测试。
- 同时检查代码、Prefab、显示文本三层，不允许只检查其中一层。
- 包装调用展开到每个实际字段；动态克隆同时检查模板和最终 hint。
- 明确拒绝 `string.Empty`、空字面量和业务数据替代调用代码。
- Prefab 保存后做 Unity 重导、专项测试、Console 检查和至少一次可见验证。
- 交付前统计调用总数、结构缺失数和语义违规数，缺失数必须为 0。

## 反模式

- 只为本次新增按钮写专项测试，不检查同类 Demo。
- 看到 `SetButtonApiHint` 调用就认定 UI 已完成。
- 依赖运行时静默行为掩盖 Prefab 缺节点。
- 只检查直接字段，跳过包装方法和动态克隆。
- 用空字符串隐藏提示，或把 SKU、状态值等业务数据当作调用代码。
- 仅凭编译通过或代码评审结论交付 Demo UI。

## 来源与验证依据

- 设计依据：[[ADR-039-base-demo-view-api-hint-split|ADR-039]]、[[PAT-105-api-hint-near-element-split|PAT-105]]。
- Prefab 实施边界：[[PAT-66-no-handcraft-prefab|PAT-66]]。
- Sample 交付闭环：[[PAT-78-sample-demo-full-flow-sop|PAT-78]]。
- 2026-08-06 全局审计：143 个 `SetButtonApiHint` / `SetFieldApiHint` 调用，其中 139 个直接字段映射、4 个变量接收者；发现 34 个直属 `ApiHintText` 缺失和 2 个 IAP 提示语义违规。
- 自动验证：`NovaFramework.Tests.Editor.DemoApiHintContractTests` 覆盖直接调用、`BindButton` 包装调用、动态模板结构及空值/业务数据违规。
