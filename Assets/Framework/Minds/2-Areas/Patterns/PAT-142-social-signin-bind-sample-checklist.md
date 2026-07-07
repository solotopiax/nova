---
id: PAT-142
title: 社交登录 Sample 接入 GameBind 配置闭环
summary: 社交登录示例接入绑定需同步代码、配置、AOT、link 与表源
category: workflow
type: pattern
status: active
date: 2026-07-06
source: cur-session
aliases:
  - PAT-142-social-signin-bind-sample-checklist
tags: [pattern, workflow, sample, gamebind, config]
related:
  - ADR-067-login-bind-save-separation
  - PAT-140-upm-package-vs-sample-dependency
---

# PAT-142：社交登录 Sample 接入 GameBind 配置闭环

## 适用场景（When）

- 在 Facebook、Google Sign-In、Apple Sign-In 等第三方登录 Sample 中增加账号绑定演示。
- Sample 代码需要调用 `Nova.Network.Kit<Bind>().BindAsync(provider, openId)`。
- 示例工程原本只展示第三方登录，现在需要补齐 GameLogin + GameBind 的运行链路。

## 核心做法（What & How）

社交登录和绑定按钮必须拆开：

- 登录按钮只负责第三方 SDK 登录，并缓存登录成功返回的 `UserId`。
- 绑定按钮不得重新触发第三方登录；它直接使用已缓存的第三方 `UserId` 调用 `BindAsync`。
- 绑定前如果当前没有游戏账号登录态，Sample 层可以先调用 `Nova.Network.Kit<Login>().Async(string.Empty, string.Empty, false)` 补齐游客登录态。
- 绑定冲突只在 Sample 中提示后续应走 `QueryConflictAsync` + `ResolveAsync`，不要把冲突裁决塞回第三方登录按钮。

接入 GameBind 的 Sample 需要按同一清单闭环：

- 代码：新增独立绑定按钮字段、按钮事件和缓存的第三方 ID 字段。
- Prefab：新增独立 BindButton，并把 `m_BindButton` 序列化引用接上。
- asmdef：Sample Runtime asmdef 引用 `NovaFramework.Kit.Network.GameBind.Runtime`；如果 Sample 为演示完整流程而主动做游客登录，也引用 `GameLogin`。
- `ConfigMaster.asset` / `ConfigRuntime.asset`：同时存在 `LoginKitConfig` 与 `BindKitConfig`，并写入 `GameAccountLogin`、`GameAccountBind`、`GameAccountBindConflict`、`GameAccountBindResolve`。
- `EnabledKits`：需要导出的 Kit 类型必须在 ConfigMaster 启用列表中，否则仅有配置实例也不会进入运行时有效配置。
- HybridCLR：AOT metadata DLL 列表要包含 `NovaFramework.Kit.Network.GameBind.Runtime.dll`；需要 GameLogin 时也确认 `NovaFramework.Kit.Network.GameLogin.Runtime.dll`。
- `link.xml`：保留 `NovaFramework.Kit.Network.GameBind.Runtime`，避免 IL2CPP 裁剪绑定协议相关类型。
- 网络表：`NetworkCmds.json` / `NetworkHostKeys.json` 补齐绑定相关命令与 `GameServer`。
- 表源：对应 Excel 源表也要同步，避免未来重新导表覆盖 JSON。
- 生成表：`TbNetworkCmds` / `TbNetworkHostKeys` 的 map convenience properties 也应和 JSON/Excel 保持一致。

## 为什么这么做（Why）

`gamebind` 运行时包按 [[ADR-067-login-bind-save-separation|ADR-067]] 保持独立，不依赖 `gamelogin`；但 Sample 为了演示“先登录游戏账号，再把三方账号绑定到当前账号”，可以在示例层同时依赖 `gamelogin` 与 `gamebind`。

这类改动容易漏掉配置链路：只改 C# 会编译通过，但运行时可能因为 `EnabledKits`、AOT metadata、`link.xml`、网络表或导表源缺失而失败。Sample 属于用户学习入口，必须保证按钮行为、配置导出、HybridCLR 保留和网络表来源同时成立。

## 反模式（Anti-patterns）

- 绑定按钮内部再次调用第三方 `LoginAsync`，导致“登录”和“绑定”语义缠在一起。
- 只改 `ConfigRuntime.asset`，不改 `ConfigMaster.asset`，下一次导出会丢失配置。
- 只改 JSON，不改 Excel 源表，下一次导表会丢失网络命令。
- 只创建 `BindKitConfig` 实例，不把类型加入 `EnabledKits`，导致导出后运行时读取不到。
- 忘记 AOT metadata DLL 或 `link.xml`，编辑器可跑但 IL2CPP/HybridCLR 构建后失效。

## 跨项目复用提示

该模式适用于所有“第三方身份提供方 + Nova GameBind”的 Sample。业务项目也可复用“第三方登录缓存 openId，绑定按钮直接消费缓存 ID”的交互分离原则，但是否自动游客登录由业务层决定。

## 来源（Origin）

- 会话日期：2026-07-06
- 关键对话节选：
  > 用户：DemoFacebookView 的绑定逻辑有点问题，我是想做绑定的时候不需要facebook重新登录一遍，这2个代码要分开，先走facebook登录，登录完毕后绑定就直接使用登录成功后的facebookID
  > 用户：给DemoGoogleSigninView 和 DemoAppleSigninView 添加上登录绑定，和facebook一样，添加一个独立按钮然后直接调用绑定协议即可
  > 用户：还有记得在ConfigMaster中的AOT元数据DLL列表中添加GameBind的DLL

## 关联

- 相关 ADR：[[ADR-067-login-bind-save-separation|ADR-067]]
- 相关 Pattern：[[PAT-140-upm-package-vs-sample-dependency|PAT-140]]
