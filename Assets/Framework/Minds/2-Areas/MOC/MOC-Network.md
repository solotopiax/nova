---
id: MOC-Network
title: 网络系统图谱
summary: Network 三管理器与 UWR 主备路由边界速查
category: module
status: active
date: 2026-06-05
aliases:
  - MOC-Network
  - 网络系统图谱
  - 网络图谱
tags: [moc, nova, network, http, websocket, unitywebrequest, runtime]
keywords: [NetworkComponent, HttpManager, WebSocketManager, NetworkManager, UnityWebRequest, HostKey, NetCmd, INetworkManager, IHttpManager, IWebSocketManager]
related:
  - "[[ADR-001-component-manager-three-layer|ADR-001]]"
  - "[[ADR-002-manager-priority-system|ADR-002]]"
  - "[[ADR-008-managerbase-internal-abstract|ADR-008]]"
  - "[[ADR-010-validation-on-consumer-side|ADR-010]]"
  - "[[ADR-012-third-party-info-isolation|ADR-012]]"
  - "[[ADR-083-uwr-primary-fallback-network|ADR-083]]"
  - "[[PAT-29-cache-component-lookup-on-init|PAT-29]]"
---

# MOC-Network：网络系统图谱

## 一句话

Network 是 Nova 里少数“一个 Component 持有多个 Manager”的模块：`HTTP / NetCmd 路由 / WebSocket` 同挂在 `NetworkComponent` 下，但职责边界是分开的。HTTP 固定使用 UnityWebRequest 与系统 DNS。

## 何时查这页

- 要分清 `Http`、`WebSocket` 与 `Network`
- 要改 NetCmd 路由、网络初始化、服务器时间逻辑
- 要判断某个网络能力该落在哪个 Manager

## 当前结构

```text
Nova.Network
  -> NetworkComponent
     -> IHttpManager       (Priority 8)
     -> INetworkManager    (Priority 10)
     -> IWebSocketManager  (Priority 9)
```

组件事实：

- `Awake()` 里反射创建 3 个 Manager
- `Start()` 里按依赖顺序初始化
- `LoadAsync()` / `LoadSync()` 是组件层的一次性 NetCmd 加载入口

## 3 个 Manager 各做什么

| Manager | 主要职责 |
|---|---|
| `HttpManager` | 基于 UnityWebRequest 与系统 DNS 的 HTTP 请求、上传和下载 |
| `WebSocketManager` | 长连接、通道、消息收发 |
| `NetworkManager` | HostKey/NetCmd 主备路由、网络状态、服务器时间 |

一句话区分：

- 要“发请求”看 `Http`
- 要“连长链”看 `WebSocket`
- 要“拿主备 URL / 查网络状态 / 服务器时间”看 `Network`

## 当前最关键的协作关系

- `NetworkManager.LoadNetCmdsAsync()` 负责把表数据变成运行时路由缓存
- `NetworkManager.ResolveNetCmdUrls()` 根据 `HostKey + NetCmd` 解析本次业务请求的主备 URL
- `Kit/NetService` 冻结请求体和请求头后交给 `HttpManager`；主地址无正式 HTTP 响应时才进入备用地址
- App 版本检查与 Asset 热更新各自维护已配置的主备语义，不进入业务 NetCmd 路由链

## 不要混淆的边界

- `NetworkComponent.LoadAsync()` 解决的是“NetCmd 是否已装载”，不是整个网络栈是否完全可用
- `INetworkManager` 暴露的是语义化网络入口，不应把底层协议细节扩散到接口层
- `Kit/NetService` 是业务协议主备编排层，不替代 3 Manager 的底层职责划分
- 单 URL 的 `Nova.Network.GetAsync/PostAsync` 不会自行推断备用域名
- 第三方 SDK 网络请求不由 Framework Network 接管

## 常见误区

- 把 `NetworkManager` 当成“所有网络能力的总实现”
- 直接在业务层依赖具体实现类而不是 `Nova.Network` 门面
- 在接口层泄漏 WebSocket 内部类型或第三方协议细节
- 收到 HTTP 4xx/5xx 后继续切换备用域名，造成业务请求重放
- 忘记 Network 是三个 Manager 并存结构，误按单 Manager 模板改造

## 先往哪看

- 改组件/管理器结构：[[ADR-001-component-manager-three-layer]]
- 改 Priority：[[ADR-002-manager-priority-system]]
- 改接口泄漏边界：[[ADR-012-third-party-info-isolation]]
- 改 UWR 与主备路由：[[ADR-083-uwr-primary-fallback-network]]

## 关联

- 图谱：[[MOC-Manager]]、[[MOC-SDK]]
