---
id: PAT-143
title: 厂商 SDK 缺公开 API 时，接入层用现有 public 方法在 Nova 层补口
summary: 源只读下用现有 public API 组合补厂商缺失能力
category: module
type: pattern
status: active
date: 2026-07-07
source: cur-session
aliases:
  - PAT-143-vendor-sdk-missing-api-nova-layer-fill
tags: [pattern, module, sdk, vendor, datamaster]
related: []
---

# PAT-143：厂商 SDK 缺公开 API 时，接入层用现有 public 方法在 Nova 层补口

## 适用场景（When）

封装第三方 SDK 时，业务需要的能力厂商没暴露公开 API（如「枚举当前主题名」），但 Core 源目录只读禁改（[[PAT-141-vendor-source-readonly|PAT-141]]），不能直接加方法或读私有字段。

## 核心做法（What & How）

在 Nova 接入层（Plugin）用厂商**已有的 public 方法**组合出所缺能力，而非改 Core：

- 案例：DataMaster 无「列出 topic_name」的公开 API，但 `ParseConfigJson`（public）返回 `DMGetParamsResponse`，其 `Params.Keys` 就是 topic_name 全集。
- 做法：`OnInitializeAsync` 时解析随包默认配置，缓存 `Params.Keys`，经接入层 `GetTopicNames()` 暴露给业务。
- 优先级：public API 组合 > 调试接口解析（仅 Dev Build，脆弱）> 反射调私有（见 [[PAT-144-reflection-private-vendor-method|PAT-144]]，最后手段）。

## 为什么这么做（Why）

- 源只读红线不能破，改 Core 会在厂商 SDK 升级时冲突丢失。
- public API 全平台可用、稳定；调试接口解析（如解析 `DebugGetAllTopicsInfo` dump 文本）仅 Dev Build 可用且依赖文本格式，脆弱。
- 本案例演进：硬编码 → dump 文本解析 → `ParseConfigJson` + Params.Keys，逐步收敛到最稳解。

## 反模式（Anti-patterns）

- 为补一个能力就改 Core / 加公开方法——破源只读红线。
- 用调试接口（`#if DEVELOPMENT_BUILD` 的 dump）当生产数据源——Release 直接失效。
- 依赖服务端响应体透出——厂商 `onSuccess` 常是无参回调，响应体落库后即丢，业务拿不到。

## 跨项目复用提示

通用于任何「源只读 + 厂商 API 不全」的第三方封装场景，不限 DataMaster。

## 来源（Origin）
- 会话日期：2026-07-07
- 关键对话节选：
  > 用户：不可以动DataMaster内部代码
  > 用户：仔细阅读文档，看看还有其他接口可以获取这个topicid吗？
  > AI：`ParseConfigJson` 是 public 的，返回 DMGetParamsResponse，Params.Keys 就是 topic_name，缓存即可，不动 Core。

## 关联
- 相关 ADR：[[ADR-071-datamaster-topicid-is-params-key|ADR-071]]
- 相关 Pattern：[[PAT-141-vendor-source-readonly|PAT-141]]、[[PAT-144-reflection-private-vendor-method|PAT-144]]、[[PAT-33-sdk-plugin-sop|PAT-33]]
