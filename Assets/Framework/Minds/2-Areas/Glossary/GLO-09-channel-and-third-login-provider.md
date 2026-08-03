---
id: GLO-09
title: 运行平台、运营渠道与第三方登录提供方
type: glossary
status: active
date: 2026-08-03
summary: 区分运行平台、运营渠道与登录提供方
category: naming
source: user-correction-and-source-verification
aliases:
  - GLO-09-channel-and-third-login-provider
  - PlatformType
  - PbNetPlatform
  - ChannelType
  - PbNetChannel
  - ThirdLoginProvider
keywords: [GLO-09, PlatformType, PbNetPlatform, ChannelType, PbNetChannel, ThirdLoginProvider, platform, channel, provider]
tags: [glossary, nova, terminology, network, login, platform, channel, provider]
related:
  - "[[ADR-067-login-bind-save-separation|ADR-067]]"
---

# GLO-09：运行平台、运营渠道与第三方登录提供方

## 定义

- `platform` / `PlatformType`：客户端运行平台类型，用于描述实际运行目标，如 Android、iOS、WebGL。
- `PbNetPlatform`：请求 Header 中的 Proto 运行平台枚举，由客户端运行平台显式映射得到。
- `channel` / `ChannelType`：客户端的游戏运营渠道类型，用于描述包体分发与运营来源，如 Google Play、App Store。
- `PbNetChannel`：请求 Header 中的 Proto 运营渠道枚举，与 `ChannelType` 的有效值保持数值一致。
- `provider` / `ThirdLoginProvider`：第三方登录提供方，用于描述提供账号认证能力的一方，对应 GameBind 协议 `provider` 字段的取值契约。

## 边界

`platform`、`channel`、`provider` 是三个独立维度：运行平台回答“客户端运行在哪里”，运营渠道回答“包体从哪里分发和运营”，登录提供方回答“账号由谁认证”。即使 Google、Apple 等名称同时出现在渠道与登录提供方中，也不能互相转换或复用类型。

| 类型 | 位置 | 用途 |
|---|---|---|
| `PlatformType` | Framework C# | Config 运行平台维度 |
| `PbNetPlatform` | `PbNetReqHeader.platform` | 将当前运行平台发送给服务端 |
| `ChannelType` | Framework C# | Config、包体分发和运营渠道维度 |
| `PbNetChannel` | `PbNetReqHeader.channel` | 将当前运营渠道发送给服务端 |
| `ThirdLoginProvider` | GameBind C# | 约束 `BindAsync` 的第三方登录提供方 |
| `int32 provider` | `PbNetBindReq.provider` | 保持服务端既有协议字段，不在 Proto 内新增第三方登录枚举 |

## 固定取值

运行平台：

- `PlatformType.None = 0`
- `Android = 1`
- `iOS = 2`
- `WebGL = 3`

`PbNetPlatform` 使用独立协议取值，由 `NetBuilder` 显式映射；不要依赖与 `PlatformType` 数值相同。

运营渠道：

- `ChannelType.None = 0` 对应 `PbNetChannel.Unspecified = 0`
- `Official = 1`
- `Google = 2`
- `Apple = 3`
- `WeChat/Wechat = 4`
- `TikTok = 5`
- `Alipay = 6`

第三方登录提供方：

- `ThirdLoginProvider.Unspecified = 0`，禁止用于实际绑定
- `Facebook = 1`
- `Google = 2`
- `Apple = 3`
- `Wechat = 4`

## 易混淆项

- Android、iOS、WebGL 是运行平台，不是运营渠道或登录提供方。
- `PbNetChannel` 不是第三方登录提供方，不能包含 Facebook 登录语义。
- GameBind 的 `provider` 不能通过强转 `PbNetChannel` 获得。
- `PbNetBindReq.provider` 保持 `int32`；强类型约束只存在于客户端 `ThirdLoginProvider` 和 `BindAsync` API。
- 文案中的 `platform` 统一称“运行平台”，`channel` 统一称“运营渠道”，第三方账号语境中的 `provider` 统一称“登录提供方”。
- 避免用“第三方登录平台”“第三方登录渠道”或笼统的“第三方登录类型”代替“第三方登录提供方”。

## 示例

```csharp
// 运行平台：客户端实际运行在 Android。
PlatformType platform = PlatformType.Android;

// 运营渠道：包体从 Google Play 分发。
ChannelType channel = ChannelType.Google;

// Header 自动从 Nova.Config.Channel 取得运营渠道。
PbNetReqHeader header = NetBuilder.BuildHeader();

// 绑定 API 显式传入第三方登录提供方。
await Nova.Network.Kit<Bind>().BindAsync(ThirdLoginProvider.Google, openid);
```

## 来源与验证

- 用户明确纠正：`platform` 表示 Android、iOS、WebGL 等运行平台；`channel` 表示 Google、Apple 等运营渠道；第三方登录统一使用 `provider` / “登录提供方”。
- 当前 `PlatformType` 源码定义为 Android、iOS、WebGL 运行平台，`NetBuilder` 将其显式映射为 `PbNetPlatform`。
- 当前 `ChannelType` 源码定义为包体分发与运营渠道。
- `pb_net_bind.proto` 的 `provider` 经确认继续保持 `int32`。
- Framework 与 GameBind 定向编译通过，错误复用引用经精确扫描清零。
