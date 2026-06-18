---
id: MOC-Network
title: 网络系统图谱
summary: Network 四管理器结构与边界速查
category: module
status: active
date: 2026-06-05
aliases:
  - MOC-Network
  - 网络系统图谱
  - 网络图谱
tags: [moc, nova, network, http, websocket, doh, runtime]
keywords: [NetworkComponent, HttpManager, WebSocketManager, NetworkManager, DoHManager, INetworkManager, IHttpManager, IWebSocketManager, IDoHManager]
related:
  - "[[ADR-001-component-manager-three-layer|ADR-001]]"
  - "[[ADR-002-manager-priority-system|ADR-002]]"
  - "[[ADR-008-managerbase-internal-abstract|ADR-008]]"
  - "[[ADR-010-validation-on-consumer-side|ADR-010]]"
  - "[[ADR-012-third-party-info-isolation|ADR-012]]"
  - "[[PAT-29-cache-component-lookup-on-init|PAT-29]]"
---

# MOC-Network：网络系统图谱

## 一句话

Network 是 Nova 里少数“一个 Component 持有多个 Manager”的模块：`DoH / HTTP / NetCmd 路由 / WebSocket` 同挂在 `NetworkComponent` 下，但职责边界是分开的。

## 何时查这页

- 要分清 `Http`、`WebSocket`、`Network`、`DoH`
- 要改 NetCmd 路由、网络初始化、服务器时间逻辑
- 要判断某个网络能力该落在哪个 Manager

## 当前结构

```text
Nova.Network
  -> NetworkComponent
     -> IDoHManager        (Priority 11)
     -> IHttpManager       (Priority 8)
     -> INetworkManager    (Priority 10)
     -> IWebSocketManager  (Priority 9)
```

组件事实：

- `Awake()` 里反射创建 4 个 Manager
- `Start()` 里按依赖顺序初始化
- `LoadAsync()` / `LoadSync()` 是组件层的一次性 NetCmd 加载入口

## 4 个 Manager 各做什么

| Manager | 主要职责 |
|---|---|
| `HttpManager` | HTTP 请求、下载、DoH 感知 |
| `WebSocketManager` | 长连接、通道、消息收发 |
| `NetworkManager` | NetCmd 路由、网络状态、服务器时间 |
| `DoHManager` | DNS-over-HTTPS、IP 预热 |

一句话区分：

- 要“发请求”看 `Http`
- 要“连长链”看 `WebSocket`
- 要“拿 URL / 查网络状态 / 服务器时间”看 `Network`
- 要“预解析域名和 IP”看 `DoH`

## 当前最关键的协作关系

- `NetworkManager.LoadNetCmdsAsync()` 负责把表数据变成运行时路由缓存
- `DoHManager` 会消费 `NetworkManager.GetAllNetCmdUrls()` 的结果做预热
- 业务侧通常先通过 `NetCmd` 取 URL，再交给 `HttpManager`

## 不要混淆的边界

- `NetworkComponent.LoadAsync()` 解决的是“NetCmd 是否已装载”，不是整个网络栈是否完全可用
- `INetworkManager` 暴露的是语义化网络入口，不应把底层协议细节扩散到接口层
- `Kit/NetService` 是上层便捷封装，不替代 4 Manager 的底层职责划分

## 常见误区

- 把 `NetworkManager` 当成“所有网络能力的总实现”
- 直接在业务层依赖具体实现类而不是 `Nova.Network` 门面
- 在接口层泄漏 WebSocket 内部类型或第三方协议细节
- 忘记 Network 是多 Manager 并存结构，误按单 Manager 模板改造

## 先往哪看

- 改组件/管理器结构：[[ADR-001-component-manager-three-layer]]
- 改 Priority：[[ADR-002-manager-priority-system]]
- 改接口泄漏边界：[[ADR-012-third-party-info-isolation]]

## 关联

- 图谱：[[MOC-Manager]]、[[MOC-SDK]]
