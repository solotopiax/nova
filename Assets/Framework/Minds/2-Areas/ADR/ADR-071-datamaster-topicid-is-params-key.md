---
id: ADR-071
title: DataMaster 读参 topicId 实为 Params 字典 key（topic_name），非 experiment.topicId
summary: 读参传 Params 字典 key，非 experiment.topicId
category: module
status: accepted
date: 2026-07-07
source: cur-session
aliases:
  - ADR-071-datamaster-topicid-is-params-key
tags: [adr, nova, module, sdk, datamaster, abtest]
supersedes: []
superseded-by: []
related: []
---

# ADR-071：DataMaster 读参 topicId 实为 Params 字典 key（topic_name），非 experiment.topicId

## 背景（Context）

封装 Starlus DataMaster SDK（远程配置 / ABTest）时，demo 读参一直返回兜底值、读原始 JSON 显示「无值」，但调试 dump 明确显示服务端已下发 `new=true`。自相矛盾的现象指向读参检索路径有问题。

厂商服务端响应体结构（同默认配置 QuickStart 文件）：

```json
{
  "params": {
    "cfa3ee9f": {                         // ← Params 字典 key（哈希格式）
      "experiment": { "topicId": "topic_VgZav6", "caseId": "" },
      "schema": { "show_start_button": 4 },
      "values": { "show_start_button": false }
    }
  }
}
```

厂商 `Core/README.md` line 34 写「`Params` 的 key 是 topicId」，同时 `experiment.topicId` 字段又叫 topicId——**同一个 "topicId" 指两个不同标识**，是混淆根源。

## 决策（Decision）

**业务读参 / 读原始 JSON / 曝光三个 API 的 `topicId` 入参，必须传 Params 字典 key（如 `cfa3ee9f`，落库为 `dm_param_table.topic_name`），不能传 `experiment.topicId`（如 `topic_VgZav6`）。**

代码依据（`Core/DataMaster.cs`，只读核实）：

- 落库：`ProcessServerConfig` 用 `topicKv.Key`（=`cfa3ee9f`）写入 `dm_param_table.topic_name`。
- 读参：`GetParamValueJson` / `GetParamValue<T>` SQL 为 `WHERE topic_name = ?`，即期望传 `cfa3ee9f`。
- 曝光：`SetTopicExposureTimeMs` SQL 同为 `WHERE topic_name = ?`。
- `experiment.topicId`（`topic_VgZav6`）落入 `dm_experiment_table.topic_id`，仅用于 `BuildExperimentsForRequest` 的上报上下文协商，**不用于任何检索**。

## 后果（Consequences）

### 正面
- 读参、读 JSON、曝光全部命中，能读到服务端下发的分组值。
- 明确了两个标识的分工，后续接入不再误传。

### 负面
- `cfa3ee9f` 由服务端生成、业务无法预知，需运行时从 SDK 获取，衍生出「接入层补 topic_name 枚举 API」的做法（见 [[PAT-143-vendor-sdk-missing-api-nova-layer-fill|PAT-143]]）。

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 硬编码 `topic_VgZav6` | 检索用 topic_name，传 experiment.topicId 永远查不到行 |
| demo 硬编码 `cfa3ee9f` | 用户否决：服务端生成值不应写死在客户端，demo 须还原真实接入场景 |

## 验证依据（Verification）

- `Core/DataMaster.cs`：`GetParamValueJson`（`WHERE topic_name = ?`）、`ProcessServerConfig`（`topicKv.Key` 落 topic_name）、`SetTopicExposureTimeMs`、`BuildExperimentsForRequest`。
- 真机日志：`GetParamsRequest (JSON)` experiments key = `cfa3ee9f`；dump 显示 `Topic [cfa3ee9f] ... show_start_button ... new=true`。

## 来源（Origin）
- 会话日期：2026-07-07
- 关键对话节选：
  > 用户：cfa3ee9f 就是sdk返回的啊，不可以用吗？
  > 用户：服务器登录成功后，拉取到的配置中没有这个cfa3ee9f吗？
  > AI：读参 SQL 是 `WHERE topic_name = ?`，topic_name 列存的是 Params 字典 key（cfa3ee9f），非 experiment.topicId。

## 关联
- 相关 ADR：[[ADR-070-sdk-enable-via-configmaster-enabledsdks|ADR-070]]
- 相关 Pattern：[[PAT-143-vendor-sdk-missing-api-nova-layer-fill|PAT-143]]、[[PAT-141-vendor-source-readonly|PAT-141]]
