---
id: ADR-068
title: 网络业务失败响应也解析并携带业务体
summary: 业务失败码也解析业务体，Fail 加带 data 重载
category: runtime
status: accepted
date: 2026-07-03
source: cur-session
aliases:
  - ADR-068-netresponse-fail-carries-data
keywords:
  - ADR-068
  - NetResponse
  - 失败响应业务体
  - BusinessData
tags: [adr, nova, runtime, network]
supersedes: []
superseded-by: []
related: []
---

# ADR-068：网络业务失败响应也解析并携带业务体

## 背景（Context）

框架 `NetService.SendAsync` 原逻辑：服务端返回业务错误码（`parseResult.Code != SUCCESS`）时直接 `NetResponse.Fail(code, msg)` 返回，**跳过业务体解析**，`resp.Data` 恒为 null。该设计隐含假设「错误码非 0 = 无业务数据」。

协议演进出现反例：账号绑定冲突 `ErrBindConflict(10402)` 在**失败码下仍返回业务体** `existing_uid`（见 [[ADR-067-login-bind-save-separation|ADR-067]]）。数据其实在 `parseResult.BusinessData` 里没丢（`NetParser.ParseResponse` 无论 Code 是否为 0 都填了 BusinessData），只是被 SendAsync 的失败分支跳过解析，导致业务侧 `resp.Data == null`、拿不到 existing_uid。

## 决策（Decision）

- `NetResponse<T>` 新增 `Fail(int errorCode, string errorMessage, T data)` 重载（原 2 参 `Fail(code, msg)` 保留，向后兼容）。
- `NetService.SendAsync` 业务错误码分支改为：若 `BusinessData` 非空则 `try parser.ParseFrom(BusinessData)`，成功则 `Fail(code, msg, data)`；解析失败或无业务体则降级为原 `Fail(code, msg)`，不影响错误码/描述透传。

## 后果（Consequences）

### 正面
- 业务侧在 `IsSuccess=false` 时也能读 `resp.Data`（服务端带了业务体时），支持「失败码携带附带信息」的协议模式。
- 向后兼容：原按 `IsSuccess`/`ErrorCode` 分支的代码零感知；失败时 Data 从「恒 null」变为「有则有值」，只增不减。

### 负面
- 极少数假设「失败时 Data 必为 null」并据此写逻辑的业务代码会受影响（正常按 IsSuccess 分支的不受影响）。
- 触及框架核心网络主链路（此前有「不动 NetService 主链路」的软约束），属协议演进的必要修复。

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 仅在 gamebind 旁路解析 existing_uid | NetService 失败分支未向上层暴露原始 BusinessData（Fail 只带 code+msg），旁路走不通，除非也改框架暴露原始字节 |
| existing_uid 改走 conflict 接口不读 bind 返回 | 服务端已在 bind 10402 返回 existing_uid，客户端读不到是纯客户端缺陷，且丢失可用信息 |

## 验证依据（Verification）

- 文件：`Assets/Framework/Scripts/Runtime/Modules/Network/Kit/NetResponse.cs`（Fail 3 参重载）、`NetService.cs`（失败分支解析 BusinessData）
- 运行时反射验证：构造 `PbNetBindResp(existing_uid=99)` 序列化 → 模拟失败分支 `parser.ParseFrom` → `Fail(10402, msg, data)` → 结果 `IsSuccess=False / ErrorCode=10402 / Data.ExistingUid=99`

## 来源（Origin）
- 会话日期：2026-07-03
- 关键对话节选：
  > 用户：点击绑定三方号后可以正常响应 10402，但 resp.Data 为 null 获取不到数据，服务器告诉我他已经返回了 existing_uid。
  > 用户（选定方向）：框架失败分支也解析 Data。

## 关联
- 相关 ADR：[[ADR-067-login-bind-save-separation|ADR-067]]、[[ADR-057-network-kit-base-sink-into-framework|ADR-057]]
