---
id: RES-004
title: 登录与三方绑定服务端业务契约
summary: Header 身份、绑定目标、裁决切号与顶号契约
category: network
status: active
type: resource
date: 2026-07-31
source: cloud-server-flow-document
author: Nova cloud server team
aliases:
  - RES-004-login-third-party-bind-server-contract
  - 登录绑定服务端流程
tags: [resource, nova, network, login, bind, openid, uid, device]
keywords: [GameLogin, GameBind, OpenID, UID, device_id, Header, 10400, 10402, 10407, final_uid, abandoned_uid]
related:
  - "[[ADR-067-login-bind-save-separation|ADR-067]]"
  - "[[ADR-068-netresponse-fail-carries-data|ADR-068]]"
---

# RES-004：登录与三方绑定服务端业务契约

## 来源

2026-07-31 由云端服务器团队提供的《账号登录与三方绑定业务流程》。该材料详细描述当前服务端中间件、登录、绑定、冲突裁决和最新设备校验的实际执行顺序，是客户端 Login/Bind 接入的服务端事实来源。

面向接入方的完整操作手册已整理到 GameBind package：

`Nova/Docs/AccountLoginAndThirdPartyBindFlow.md`

## 稳定契约

### Header 是当前身份声明

- 请求 Header 的 UID/OpenID 表示客户端当前会话已经拥有的身份。
- Bind、冲突查询和裁决的目标 OpenID 只属于业务 Body，不能覆盖 Header OpenID。
- Header 同时携带渠道和 OpenID 时，服务端先验证 OpenID 绑定 UID 是否等于 `head.uid`；不一致返回 `10407`，不会进入后续业务。
- 响应 Header 的 OpenID 是请求身份回显，不是绑定成功或裁决后的权威归属查询结果。

### 业务响应决定身份变化

- Login 成功后，客户端使用登录业务响应 UID 建立会话。
- Bind 成功后，目标 OpenID 已归属当前 UID，客户端可写入进程内 OpenID。
- Resolve 成功后，客户端必须用业务响应 `final_uid` 覆盖当前 UID，并把目标 OpenID 作为当前身份。
- `abandoned_uid` 的游戏数据保留，但不再持有目标 OpenID；本地会话和临时数据应隔离。

### 最新设备与顶号

- 每次成功登录都会把 UID 的最新 `device_id` 更新为当前设备。
- 登录本身是成为最新设备的入口，不会因为设备变化而拒绝本次登录。
- 旧设备在之后访问受保护接口时收到 `10400 ErrKicked`。
- Resolve 成功后，服务端也会把当前设备设为 `final_uid` 的最新设备。

### Bind 与 Save 边界

- 服务端只裁决 OpenID 归属，不自动合并、迁移或删除两个 UID 的游戏数据。
- `choice=guest` 后，业务层上传本地存档覆盖云端。
- `choice=existing` 后，客户端身份已切到 `final_uid`，业务层拉云端存档覆盖本地。

## 适用范围

- Nova Framework 的 GameLogin、GameBind、GameSave 客户端编排。
- `PbNetReqHeader.openid` 与 `PbNetRespHeader.openid` 的业务语义解释。
- `10400`、`10402`、`10407` 的客户端动作与测试设计。

不覆盖第三方 token 真伪校验；该能力由上层第三方授权流程负责。

## 可信度与验证

- 可信度：高，来源为当前云端服务器流程说明。
- 客户端验证：Bind 三段请求不再把目标 OpenID 写入 Header；Login/Bind/Resolve 按业务成功结果更新 `NetService` 身份。
- 顶号验收序列：A 登录 → B 登录同 UID → A 不重登访问受保护接口应得 `10400`。

## 关联

- 架构职责：[[ADR-067-login-bind-save-separation|ADR-067]]
- 失败响应业务体：[[ADR-068-netresponse-fail-carries-data|ADR-068]]
