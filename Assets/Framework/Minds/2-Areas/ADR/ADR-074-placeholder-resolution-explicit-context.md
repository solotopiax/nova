---
id: ADR-074
title: 跨 Editor Runtime 导出链的占位符解析统一采用显式上下文
status: accepted
date: 2026-07-24
summary: 占位符统一算法与多环境显式上下文
category: arch
aliases:
  - placeholder-resolution-explicit-context
keywords:
  - ADR-074
  - PlaceholderContext
  - Util.Placeholder
  - 占位符显式上下文
  - ConfigMasterSO
  - ConfigRuntimeSO
tags:
  - adr
  - nova
  - framework
  - config
  - placeholder
supersedes: []
superseded-by: []
related:
  - "[[ADR-020-assembly-dependency-direction|ADR-020]]"
  - "[[ADR-025-yooasset-url-template-placeholders|ADR-025]]"
  - "[[ADR-056-runtimeprovider-config-select-via-workspaceactive|ADR-056]]"
  - "[[PAT-27-config-no-serialize|PAT-27]]"
---

# ADR-074：跨 Editor Runtime 导出链的占位符解析统一采用显式上下文

## 背景

Pipify 通知、CDN 路径和 Runtime URL 都需要替换 Platform、Channel、Package、Version 等标准占位符，
但它们所处生命周期不同：Editor 以当前激活 `ConfigMasterSO` 为真相源，Runtime 在配置加载后以
`ConfigRuntimeSO` 为真相源，导出器还可能处理一个并非当前选中格子的目标坐标。若解析器自行查找
全局配置，会引入 Editor/Runtime 反向依赖、启动期循环依赖和导出坐标漂移。

## 决策

1. 统一的替换算法放在 Runtime 程序集的 `Util.Placeholder`，只消费不可变的 `PlaceholderContext`。
2. `PlaceholderContext` 显式承载 Platform、Channel、Package、Version 和解析时刻，不在内部查找配置。
3. Editor 调用方通过 `EditorUtil.Placeholder` 从 `WorkspaceActive` 锚定的 `ConfigMasterSO` 构造上下文。
4. Runtime 在 `ConfigRuntimeSO` 已加载后从导出快照构造上下文；Package 等消费者特有值由调用方传入。
5. 导出器使用显式 Platform/Channel 重载构造目标坐标上下文，只对明确要求固化的字段做序列化替换，
   不遍历或改写所有字符串配置。
6. 启动早期无法依赖 `ConfigRuntimeSO` 的链路继续由调用方显式提供编译平台等值，不建立配置依赖。
7. 标准占位符为 `{Platform}`、`{Channel}`、`{Package}`、`{Version}`、`{Time}`；未知占位符保持原样。
   `{Time}` 使用 24 小时制 `yyyy-MM-dd-HH-mm-ss`，语义为本次解析时刻。

## 后果

### 正面

- Editor、Runtime 与导出链共享完全一致的大小写敏感替换语义。
- 配置来源和解析时刻可见、可测试，不依赖隐式全局状态。
- Runtime 不引用 Editor，导出目标坐标也不会受 ConfigWindow 当前选中状态污染。
- 新消费者只需构造上下文即可复用机制，未知扩展占位符不会被破坏。

### 负面

- 调用方必须明确提供 Package、Version 和 Time，样板参数略多。
- 旧的专用模板解析器需要按风险逐步迁移，不能一次性替换所有历史路径逻辑。
- 在导出阶段解析 `{Time}` 会固化为导出时间，调用方必须确认这正是字段所需语义。

## 被排除方案

| 方案 | 否决理由 |
|---|---|
| 解析器自动查找 ConfigMasterSO 或 ConfigRuntimeSO | 生命周期不明确，并会破坏程序集依赖方向或形成启动循环依赖 |
| 导出时递归替换所有字符串字段 | 会破坏需要保留到 Runtime 再解析的模板原文，也无法表达不同字段的固化时机 |
| Editor 与 Runtime 各维护一套 Replace 链 | 占位符集合、格式和未知 token 行为会持续漂移 |
| 把解析行为放进 ConfigMasterSO / ConfigRuntimeSO | 配置对象应保持数据化，行为属于消费侧 |

## 验证依据

- 实现：`Assets/Framework/Scripts/Runtime/Core/Util/Util.Placeholder/Util.Placeholder.cs`
- Editor 适配器：`Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.Placeholder/EditorUtil.Placeholder.cs`
- 首个消费方：`PipifySteps.Notification.cs`，发送飞书消息前使用当前 ConfigMaster 上下文解析。
- EditMode 测试覆盖标准 token、未知 token、24 小时时间格式、ConfigRuntime 来源、ConfigMaster 当前坐标、
  显式导出坐标以及 Pipify HelpBox 元数据。

## 关联

- [[ADR-020-assembly-dependency-direction|ADR-020]]：Runtime 不反向依赖 Editor。
- [[ADR-025-yooasset-url-template-placeholders|ADR-025]]：启动早期 URL 仍允许显式提供平台，不依赖 Runtime Config。
- [[ADR-056-runtimeprovider-config-select-via-workspaceactive|ADR-056]]：Editor 当前配置选择继续收口 WorkspaceActive。
- [[PAT-27-config-no-serialize|PAT-27]]：Config 保持数据化，解析行为放在消费侧。
