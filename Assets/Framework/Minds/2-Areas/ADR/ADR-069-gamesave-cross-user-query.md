---
id: ADR-069
title: 云存档支持按 target_uid 跨用户查询
summary: GetGameData 加 target_uid，为空查自身、有值查指定 uid，鉴权归服务端
category: module
status: accepted
date: 2026-07-03
source: cur-session
aliases:
  - ADR-069-gamesave-cross-user-query
tags: [adr, nova, module, gamesave, network]
supersedes: []
superseded-by: []
related: []
---

# ADR-069：云存档支持按 target_uid 跨用户查询

## 背景（Context）

`gamesave` 的获取存档接口原本只能拉取当前登录用户自身的存档——`PbNetGetGameDataReq` 无目标 uid 字段，身份完全由 `NetBuilder.BuildHeader()` 填入的 `Header.Uid`（当前登录态）决定。

绑定冲突二选一场景（见 [[ADR-067-login-bind-save-separation|ADR-067]]）需要 guest 侧查看 existing 账号（另一个 uid）的存档进度以辅助玩家决策；此外通用调试/客服场景也可能需要查指定用户存档。原接口无法表达"查别人的存档"。

## 决策（Decision）

- `PbNetGetGameDataReq` 新增 `string target_uid`（字段号 4）：为空时拉取 `Header.Uid` 自身存档（兼容原行为）；有值时拉取该 uid 的存档。
- `Save` 新增 `GetFullAsync(string targetUid)` 重载；原 `GetFullAsync()` 保留（内部传空 target_uid）。
- **跨用户查询的权限校验完全归服务端**：客户端只负责透传 target_uid，服务端必须校验请求方是否有权查询该 uid（如是否处于绑定冲突关系），裸放行是隐私/作弊漏洞。

## 后果（Consequences）

### 正面
- 绑定冲突二选一可查 existing 账号存档进度；调试/客服可查指定用户存档。
- 向后兼容：target_uid 为空等价于原 `GetFullAsync()`，现有调用零影响。

### 负面
- 引入跨用户数据访问面，安全责任压在服务端鉴权上——服务端漏做校验即成隐私/作弊漏洞。客户端侧无法兜底。

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 客户端临时切登录态到目标 uid 再 GetFull | 切登录态有副作用（影响后续请求身份），且需目标 uid 的登录凭证，不适合"查别人" |
| 为绑定冲突单开一个专用查存档接口 | target_uid 是通用能力，专用接口重复造轮子；通用字段 + 服务端鉴权更简洁 |

## 验证依据（Verification）

- proto：`UPMPackages/com.solotopia.nova.framework.kit.network.gamesave/Nova/Protos/pb_net_save.proto`（PbNetGetGameDataReq.target_uid）
- API：`Save.cs` 的 `GetFullAsync(string targetUid)` 重载 + `SendGetFullAsync(cmdRow, targetUid)`
- Demo：GameSaveDemo 与 GameBindDemo 均加「查询指定 uid 存档」input+button 演示

## 来源（Origin）
- 会话日期：2026-07-03
- 关键对话节选：
  > 用户：在"查询冲突详情"下面新增一个按钮，查询云端指定用户 uid 的存档，看看目前的 gamesave kit 是否支持？如果不支持，看看需要如何调整协议。
  > AI：现不支持（只能查自己）；需 PbNetGetGameDataReq 加 target_uid，且跨用户查询必须服务端鉴权。

## 关联
- 相关 ADR：[[ADR-067-login-bind-save-separation|ADR-067]]、[[ADR-043-gamesave-full-explicit-flag|ADR-043]]、[[ADR-045-setfull-value-via-datas0|ADR-045]]
