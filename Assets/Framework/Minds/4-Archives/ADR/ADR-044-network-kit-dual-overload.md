---
id: ADR-044
title: 业务网络 Service 入口统一双重载（cmdName + cmdRow）
summary: 业务 Kit 入口提供 cmdName/cmdRow 双重载
category: module
status: superseded
date: 2026-05-27
aliases:
  - ADR-044
keywords:
  - ADR-044
  - 业务网络 Service 入口统一双重载（cmdName + cmdRow）
tags:
  - adr
  - nova
  - network
  - kit
supersedes: []
superseded-by:
  - ADR-053
related: []
---

# ADR-044：业务网络 Service 入口统一双重载（cmdName + cmdRow）

## 背景（Context）

`com.solotopia.nova.framework.kit.network.*` 系列业务 Service（`Login`、`GameSave` 等）通过 `Nova.Network.Kit<T>()` 暴露给业务侧。早期入口只接受 `INetworkCmdRow cmdRow`，调用方必须自己先 `Nova.Network.ResolveNetCmdRow(cmdName)` 拿到行数据再传入，模板代码冗余且易抄错。

约束：
- 业务侧最常见的写法就是按 NetCmd 名称发起请求
- 框架侧已有 `Nova.Network.ResolveNetCmdRow(string)` 门面 API
- 仍需保留 `INetworkCmdRow` 重载，便于复用同一行数据多次发起请求或预解析快路径失败

## 决策（Decision）

每个业务 Service 的每条业务入口方法**同时提供两版重载**：

1. **`cmdName` 重载**：`UniTask<NetResponse<T>> XxxAsync(string cmdName, ...)`
   - 内部以 `Nova.Network.ResolveNetCmdRow(cmdName)` 解析
   - **直通 `return`** 调到 `cmdRow` 重载，**禁止加 `async` 多包一层状态机**（避免多生成一层状态机 + 异常栈包装）

2. **`cmdRow` 重载**：`UniTask<NetResponse<T>> XxxAsync(INetworkCmdRow cmdRow, ...)`
   - 真实业务实现入口
   - 调用方手持 `INetworkCmdRow`（可复用、可显式快路径失败）

落地范围：
- `Login.Async(cmdName/cmdRow, channel, openId, ...)`
- `GameSave.GetAsync(cmdName/cmdRow, key/keys)`
- `GameSave.SetAsync(cmdName/cmdRow, key, value / keys, values)`
- `GameSave.GetFullAsync(cmdName/cmdRow)`
- `GameSave.SetFullAsync(cmdName/cmdRow, value)`
- 后续新增的所有业务 Kit 入口必须遵循

## 后果（Consequences）

### 正面
- 业务侧最常用的 cmdName 写法零模板代码
- 进阶用法（手持 cmdRow 复用、预解析）保留出口
- API 形态在 kit.network.* 内可预测，跨包复刻无歧义

### 负面
- 每条业务方法 API 数量翻倍，类签名长
- 未讨论：跨 Kit 接口数膨胀的可维护性阈值

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 仅保留 `cmdRow` 重载 | 业务侧每次都要写一行 `Nova.Network.ResolveNetCmdRow`，模板冗余 |
| 仅保留 `cmdName` 重载 | 失去预解析复用；无法显式表达"行数据非法时立即失败"语义 |
| `cmdName` 重载内部用 `async/await` 转发 | 多生成一层状态机 + 异常栈包装；纯转发不需要状态机 |

## 验证依据（Verification）

- `UPMPackages/com.solotopia.nova.framework.kit.network.login/Nova/Scripts/Runtime/Login.cs`
- `UPMPackages/com.solotopia.nova.framework.kit.network.gamesave/Nova/Scripts/Runtime/GameSave.cs`
- 文档：两包 `Nova/Docs/*.md` API 表
- grep：`ResolveNetCmdRow` 在 cmdName 重载内部唯一出现点

## 来源（Origin）

- 会话日期：2026-05-27
- 关键对话节选：
  > 用户："新增一套根据cmdName参数调用的接口：GetAsync(cmdName)，Set接口也一样"
  > 用户："同样的方式也加一下 Login.cs 中 Async 的 cmdName 的版本"

## 关联

- 规范落点：待评审（候选：统一工程约束追加"转发型重载禁加 async"条）
- 相关 ADR：[[ADR-043-gamesave-full-explicit-flag|ADR-043]]
- 相关 Pattern：—
