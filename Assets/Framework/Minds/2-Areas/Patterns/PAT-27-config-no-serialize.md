---
id: PAT-27
title: Config 类型保持数据化，不承载序列化行为
type: pattern
status: active
date: 2026-06-05
summary: Config 只承载数据不承载行为
category: config
aliases:
  - PAT-27-config-no-serialize
keywords:
  - PAT-27
  - Config 数据化
tags:
  - pattern
  - config
  - serialization
  - nova
related:
  - "[[ADR-010-validation-on-consumer-side|ADR-010]]"
  - "[[ADR-012-third-party-info-isolation|ADR-012]]"
---

# PAT-27：Config 类型保持数据化，不承载序列化行为

## 适用场景

- 新增或修改 `*ManagerConfig`、SDK/Kit 配置类、表配置项、路径配置项
- 评估配置类里是否应该加入校验、回调、派生字段或运行时逻辑
- 处理 `ConfigMasterSO`、`ConfigRuntimeSO` 与具体配置项之间的职责边界

## 核心规则

- 叶子 Config 的首要职责是“承载数据”。
- 配置解析、维度选择、导出拼装、运行期校验，不应下沉到每个叶子 Config。
- 如果确实需要序列化重建或结构整理，应优先放在聚合根，而不是散落到各个配置项里。

## 当前代码事实

- 大多数 `*ManagerConfig`、`CommonConfig`、`SDKPluginEntry`、`DllAssetEntry` 等类型都是轻量数据容器。
- `ConfigMasterSO` 是明确的聚合根例外：它承担矩阵索引与反序列化重建。
- `ConfigRuntimeSO` 承担运行时导出物职责，并通过 `[SerializeReference]` 保存多态 SDK/Kit 配置列表。

## 设计含义

- 配置类越像“会执行逻辑的对象”，导出链路和 Inspector 链路越难维护。
- 把行为放在消费侧，可以让同一份配置同时服务 Editor、导出、运行时三条链路。
- 配置类应尽量保持可读、可导出、可替换，而不是演变成隐式状态机。

## 反模式

- 在每个叶子 Config 上实现自己的序列化回调
- 为了“省事”把消费侧校验塞进配置对象内部
- 在配置对象里缓存运行时句柄、临时状态或编辑期上下文

## 关联

- [[ADR-010-validation-on-consumer-side|ADR-010]]
- `Docs` 中与 `ConfigMasterSO`、`ConfigRuntimeSO` 对应的事实文档
