---
id: ADR-079
title: 业务 API 双域名与 DoH 指定连接 IP 采用传输能力门控
summary: 业务双域名按传输能力使用 DoH IP
category: module
status: accepted
date: 2026-08-13
aliases:
  - ADR-079-business-dual-domain-doh-ip-routing
keywords:
  - ADR-079
  - 业务 API 双域名
  - DoH IP 路由
  - SetIPAddress
  - FallbackValue
  - 主备域名
tags: [adr, nova, network, doh, besthttp, dual-domain]
supersedes: []
superseded-by: []
related:
  - "[[ADR-057-network-kit-base-sink-into-framework|ADR-057]]"
  - "[[ADR-068-netresponse-fail-carries-data|ADR-068]]"
  - "[[MOC-Network]]"
---

# ADR-079：业务 API 双域名与 DoH 指定连接 IP 采用传输能力门控

## 背景（Context）

Nova 的业务 API 需要在单一业务调用入口下同时具备主域名、备用域名和 DoH IP 轮换能力，并保持业务层无感知。直接把 HTTPS URL 改写成 IP 会改变 TLS SNI 和证书校验名，因此不能作为通用实现。

该能力只属于 Framework Network 中通过 `HostKey + NetCmd` 发起的业务请求。资源下载/CDN、热更新和第三方 SDK 各自保留原有网络机制，不能被这条路由链吸收。

## 决策（Decision）

### 1. 主备域名进入同一业务请求链

`NetworkHostKeys` 保留 `Value` 作为主域名，并新增 `FallbackValue` 作为备用域名。两者格式相同：末尾不得包含斜杠或空格；`NetCmd.Path` 非空时必须以斜杠开头。

框架冻结本次请求字节与请求头后，按以下顺序执行物理请求：

```text
P1 → B1 → P2 → B2 → … → 主域名系统 DNS → 备用域名系统 DNS
```

主备域名的 DoH 查询并行执行；各域名内部只查询 A 记录，并递归处理响应中的 CNAME 链，不查询 AAAA。相同 IP 去重但保持查询结果顺序。

### 2. 每个候选独享超时，正式 HTTP 响应立即结束

每个 IP 或系统 DNS 候选独享现有 `ConnectTimeout` 与 `RequestTimeout`，不共享整条请求链的总时限。服务器返回任何正式 HTTP 响应后立即结束轮换；业务成功或业务失败都属于网络通信成功。

只有 DNS、TCP、TLS、证书、超时或未获得正式响应等通信失败才继续下一个候选。中间轮换输出 `【继续重试】` Warning；全部结束后按是否可能到达服务器输出 `【通信失败】` 或 `【结果未确认】` Error。

本版不增加 `operationid`。框架只保证同一轮主备/IP 重试复用相同请求数据；有副作用的接口仍需由既有业务协议保证重复请求安全。

### 3. DoH 配置与缓存使用明确边界

- 单域名 DoH 解析链超时时间小于等于 `0` 时无限等待；停用 DoH 必须关闭独立开关。
- 单域名最多使用 IP 数量默认 `3`；小于等于 `0` 时使用全部结果。
- 刷新成功时以新 IP 列表整体替换缓存；刷新失败时删除旧列表，避免继续使用过期地址。
- IP 全部尝试完后，主域名和备用域名分别以系统 DNS 作为各自最终兜底。

### 4. HTTPS 指定 IP 必须经过传输能力门

内部 BestHTTP fork 提供 `HTTPRequest.SetIPAddress(IPAddress[])`。请求 URL、HTTP Host、TLS SNI 和证书校验名继续使用原域名；注入 IP 只替换 DNS 到 TCP 的连接目标。不同 IP 使用不同连接池键，跨域重定向清除旧 IP，代理请求仍连接代理地址。

Nova 的 BestHTTP 适配包在进程内只反射检测一次该方法：

- Inspector 已启用 DoH 且检测成功时，才启用业务 HTTPS IP 注入。
- 官方 BestHTTP 没有该方法、未安装 BestHTTP、运行于 WebGL 或调用异常时，输出明确的 Nova Warning，在本进程关闭 DoH，并使用原域名与系统 DNS。
- Framework 核心不依赖 BestHTTP；没有可选适配包时继续使用 UnityWebRequest。

内部 fork 专属遥测同样通过只含 BCL 类型的反射委托接入。官方原版没有该委托时静默跳过遥测，不影响普通网络请求。

## 后果（Consequences）

### 正面

- 业务层不需要决定重试、备用域名或 IP 轮换策略。
- HTTPS 始终保留原域名的 TLS 安全语义。
- 官方 BestHTTP、UnityWebRequest 和 WebGL 在能力不足时有明确、可理解的降级行为。
- 主备域名、DoH IP 和系统 DNS 的尝试顺序稳定，日志分类可用于弱网分析。

### 代价与限制

- 候选数量越多，最坏等待时间按每个候选独立超时累加。
- 本版只消费 IPv4 A 记录，不提供 IPv6 路由。
- 未引入跨物理请求的操作编号，框架不能单独证明有副作用接口的服务端幂等性。
- 该能力要求版本组合至少为 Nova Framework `0.6.13`、BestHTTP 适配包 `0.1.7` 与内部 BestHTTP `3.0.20`；官方 BestHTTP 仍按能力门自动降级到系统 DNS。

## 被排除方案（Rejected Alternatives）

- **把 HTTPS URL 直接替换成 IP**：会改变 SNI 和证书校验名，不能作为通用方案。
- **让业务层逐接口声明重试策略**：增加重复决策和接入成本，不符合底层统一路由目标。
- **主备域名共享整条请求链超时**：后续候选会因剩余时间不足被污染为超时，降低错误数据可信度。
- **通过 DoH 超时值关闭 DoH**：配置语义混杂；关闭必须使用独立开关。
- **把资源/CDN、热更新或第三方 SDK 纳入同一链**：越过既有模块边界并破坏各自稳定机制。

## 验证依据（Verification）

- 本轮两次定向执行 `dotnet build NovaFramework.Runtime.Tests.Editor.csproj --no-restore` 均退出码为 `0`，没有编译错误。
- Unity EditMode 定向类 `NovaFramework.Tests.Editor.DoHConsumerRoutingTests` 本轮通过 `19/19`，覆盖同 hostname 全部 URL 快照的替换/清除、异常域名隔离，以及资源与 WebSocket 不进入 DoH 路由。
- Nova Framework `0.6.13`、BestHTTP 适配包 `0.1.7` 与内部 BestHTTP `3.0.20` 已完成正式发布；Nova2 当前本地依赖也已对齐到该版本组合。
- 发布闭环对应 release tag 为 `upm-release-2026.08.14-03`。
- Android 真机 `SM_G9860` 使用 Development APK 验证主备切换：主路线首个 DoH IP 被故障注入为回环地址后连接失败，框架继续备用路线并收到 HTTP `200`；整链埋点为 `1 attempt + 1 error + 1 end`，三类事件共用同一个 `best_http_chain_id`。
- 同一设备验证 DoH 全失败后的系统 DNS 兜底：两个 DoH IP 依次故障后，第 3 个候选明确进入 `system_dns` 并收到 HTTP `200`；整链埋点为 `1 attempt + 2 errors + 1 end`，全部事件共用同一个 `best_http_chain_id`。

## 当前事实来源（Sources）

- `Assets/Framework/Docs/Runtime/Modules/Network/HttpManager/HttpManager.md`
- `Assets/Framework/Docs/Runtime/Modules/Network/DoHManager/DoHManager.md`
- `Assets/Framework/Docs/Runtime/Modules/Network/NetworkManager/NetworkManager.md`
- `UPMPackages/com.solotopia.nova.framework.besthttp/README.md`
