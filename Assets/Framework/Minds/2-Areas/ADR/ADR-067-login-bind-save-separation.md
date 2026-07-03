---
id: ADR-067
title: 登录/绑定/云存档三端职责分离
summary: login 鉴权 / bind 独立包裁决归属 / 覆盖交 save 编排
category: arch
status: accepted
date: 2026-07-02
source: cur-session
aliases:
  - ADR-067-login-bind-save-separation
tags: [adr, nova, arch, module, network]
supersedes: []
superseded-by: []
related: []
---

# ADR-067：登录/绑定/云存档三端职责分离

## 背景（Context）

原设计把"账号归属冲突"语义同时塞进登录协议与绑定协议：登录响应 `PbNetLoginResp` 携带 `guest_summary` / `existing_summary`（仅 10402 时有值），绑定二选一 `BindResolveAsync` 又内建在 gamelogin 包的 `Login` 类里。这导致三处职责耦合：

1. 登录协议被"冲突展示"污染——99% 的登录里这两个 summary 字段是空的。
2. `guest_summary` 冗余——冲突仅在双方都有进度时触发，guest（本号）必有本地存档，客户端自己展示即可，无需服务器回传。
3. 二选一 `resolve` 硬规定"不合并数据"，把本属云存档的"数据覆盖决策"焊死在绑定协议里。

## 决策（Decision）

三个 Kit 职责正交，编排权上移业务层：

- **gamelogin**：只鉴权 + 取 uid。`PbNetLoginResp` 删 `guest_summary` / `existing_summary`；`Login.Async` 的 `openId` 只"读"绑定关系找 uid 登入，未绑返回 `ErrAccountNotFound(10404)`，不做绑定副作用。删除 `Login.BindResolveAsync` / `LoginKitConfig.BindResolveCmdName`；`LoginErrorCode` 移除绑定码 10401/10402/10403（保留 10400 顶号、10404）。
- **gamebind（新建包 `com.solotopia.nova.framework.kit.network.gamebind`）**：只做账号归属裁决。三段独立协议 `BindAsync`（绑定，冲突返 10402+existing_uid）→ `QueryConflictAsync`（拉 existing 进度摘要）→ `ResolveAsync`（纯裁决，只返 final_uid + abandoned_uid，不碰数据、不碰登录态）。`BindSummary` 迁本包，uid 改 string 对齐主 uid，新增 extra/timestamp 字段。`BindErrorCode` 收录 10400/10401/10402/10403/10406（10406=ErrBindBusy 行锁竞争可重试；10402 在 resolve 并发复核到归属变化时也可能返回并提示重试）。
- **gamesave**：proto 与 API 完全不动。数据覆盖由业务层编排：`choice=guest` → `SetFullAsync`（本地覆盖云端）；`choice=existing` → 切登录态后 `GetFullAsync`（云端覆盖本地）。

三包平级、均只依赖主框架，互不依赖。

> **包依赖 vs sample 依赖的边界**：`gamebind` 运行时包**不依赖** `gamelogin`（`Bind` 零引用 `Login` 类型，登录态由宿主提供）——这是职责分离的硬约束，不可为图方便加包依赖。但 `GameBindDemo` **示例工程**为演示「先登录→再绑定」完整流程，其 sample asmdef 引用 `gamelogin`，故示例层依赖 `gamelogin` 包（在 gamebind 的 README / nova-samples.json 显式声明，提示项目组导入示例前需同装 gamelogin）。包级职责分离与示例级完整演示两不矛盾。

## 后果（Consequences）

### 正面
- 三协议纯净，各自单一职责，登录协议不再随冲突摘要膨胀。
- 数据覆盖交业务层后，业务侧可自定义合并策略（如保留本地 Bag + 云端 Quest），不被"硬二选一"约束。
- bind 独立成包，只做游客登录的游戏不引 gamebind。

### 负面
- 首次三方登录变两步（注册 + 绑定），换取 login 不做绑定副作用。
- 冲突查询多一次网络往返（bind 10402 后需再调 QueryConflictAsync 拉摘要）。

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 维持登录 resp 携带双 summary | 登录协议兼职冲突载体，99% 请求两字段为空，职责不清 |
| bind 复用 login 协议（原 BindAsync 走 PbNetLoginReq） | 语义混淆，force_new_account 被迫身兼绑定/切号双职 |
| resolve 内建数据不合并 | 越界干 save 的活，且焊死无法灵活合并 |
| bind 留在 gamelogin 包内 | 登录与绑定强耦合，无法按需裁剪 |

## 验证依据（Verification）

- 设计文档：`Docs/superpowers/specs/2026-07-02-login-bind-save-separation-design.md`（含完整 proto + 服务端接口契约 + 编排流程）
- 落地包：`UPMPackages/com.solotopia.nova.framework.kit.network.gamebind/`（Bind 三段式 + BindKitConfig + BindErrorCode + pb_net_bind.proto）
- 演示：`Assets/Samples/GameBindDemo/`（DemoGameBindView 四步绑定流程，Play Mode 验证通过）
- grep 校验：全项目无 `Login.BindResolveAsync` / `LoginErrorCode.ErrBindConflict` / `LoginResp.GuestSummary` 残留引用

## 来源（Origin）
- 会话日期：2026-07-02
- 关键对话节选：
  > 用户：guest_summary 已经是当前账号的摘要，为什么还要通过服务器返回？这个很不合理。PbNetBindResolveReq 的动作是否可以通过云存档由业务层做。甚至说 bind 可以单独作为一个 kit 包。
  > AI：三者应各管一摊——login 只鉴权拿 uid、bind 只裁决归属、save 只决定数据覆盖方向，编排上移业务层。

## 关联
- 设计规格：`Docs/superpowers/specs/2026-07-02-login-bind-save-separation-design.md`
- 相关 ADR：[[ADR-043-gamesave-full-explicit-flag|ADR-043]]
