---
id: PAT-134
title: 间歇性网络故障：吞异常≠治本，'突然好了'是反向证据
type: pattern
status: active
summary: 间歇网络故障吞异常非治本，突然好了是反向证据
category: review
date: 2026-06-03
aliases:
  - PAT-134-intermittent-network-fail-diagnosis
keywords:
  - PAT-134
  - 间歇性网络故障：吞异常≠治本，'突然好了'是反向证据
  - PAT-134-intermittent-network-fail-diagnosis
tags:
  - pattern
  - nova
  - network
  - http2
  - methodology
---

# PAT-134：间歇性网络故障：吞异常≠治本，'突然好了'是反向证据

## 适用场景

排查**间歇性（时好时坏）网络/长连接故障**时使用，典型如公网 HTTPS + HTTP/2 长连接登录偶发 `Connection closed unexpectedly / code=-1`。当出现下列任一信号，套用本模式：
- 无法稳定复现，只能静态审查；
- 在 teardown 回调（如 `OnConnectionClosed`）里看到异常（如 `ObjectDisposedException` / `Safe handle has been closed`）；
- 改完代码后「突然就好了」。

## 核心做法

1. **区分「伴生噪音」与「根因」**：在连接已关闭之后触发的异常（`OnConnectionClosed → Set()` 抛 `ObjectDisposedException`）是 teardown 的副产物，吞掉它只消除日志/崩溃，**连接该关还是关，请求该失败还是失败**。先看调用栈里异常发生在「关闭之前」还是「关闭之后」，后者必为噪音。
2. **「突然好了」当作未解决的证据**：间歇性故障不会自愈，一次成功只代表恰好用到热连接/新连接，没踩到被服务端 idle 回收的 stale 连接。根因（服务端/网关 HTTP/2 idle timeout 单方面回收长连接）未动。
3. **对纯网络层失败做客户端重试兜底**：对 `code=-1` 这类非业务拒绝的失败，在 NetService 层做有限次（1~2 次）自动重试，第二次大概率新建连接即成——这是对「时好时坏」唯一务实有效的修法。
4. **验证前先确认改动是否进了运行时**：本地 `UPMPackages/...`（file: 源）与业务工程实际加载的 `Library/PackageCache/...@hash`（registry/缓存包）是两份文件；不 bump version、不删 PackageCache 副本时改动不会生效，否则「还报错」会被误判为「修错方向」。

## 验证顺序

1. 先确认改动真的进了运行时包。
2. 再区分关闭后噪音与关闭前根因。
3. 最后验证重试是否只兜住网络层失败，而没有吞掉业务拒绝。

## 反模式

- 把「吞掉 teardown 异常」表述为「治本/根治登录失败」——实为根治崩溃噪音，二者混为一谈。
- 无法复现就从假设直接跳到修复，不验证因果。
- 看到「好了」就收工，不区分是修复生效还是落入了「好」的时间窗。
