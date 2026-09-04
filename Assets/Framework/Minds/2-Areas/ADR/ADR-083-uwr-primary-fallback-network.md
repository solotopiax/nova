---
id: ADR-083
title: Nova 网络统一采用 UnityWebRequest 与共享主备执行机制
summary: UWR 系统 DNS 承载三模块共享主备执行机制
category: module
status: accepted
date: 2026-09-01
aliases:
  - ADR-083-uwr-primary-fallback-network
keywords:
  - ADR-083
  - UnityWebRequest
  - 主备域名
  - system DNS
  - HttpFallbackPlanner
  - RetryRequestCount
  - RetryDownloadCount
  - EnableUWRTracks
tags: [adr, nova, network, unitywebrequest, dual-domain]
supersedes:
  - "[[ADR-079-business-dual-domain-doh-ip-routing|ADR-079]]"
superseded-by: []
related:
  - "[[ADR-057-network-kit-base-sink-into-framework|ADR-057]]"
  - "[[ADR-068-netresponse-fail-carries-data|ADR-068]]"
  - "[[ADR-076-startup-whitelist-metadata-routing|ADR-076]]"
  - "[[ADR-080-asset-package-sticky-fallback-routing|ADR-080]]"
  - "[[MOC-Network]]"
---

# ADR-083：Nova 网络统一采用 UnityWebRequest 与共享主备执行机制

## 背景（Context）

线上项目验证表明，BestHTTP + DoH 路径的网络联通问题明显多于 UnityWebRequest。继续保留可选传输、DoH IP 注入和两套诊断链会扩大线上差异、配置面与维护成本。

App 版本检查、Asset 热更新以及 Framework 与业务协议请求都需要主备、轮次、重试和最近成功能力。三类调用需要共享确定的执行算法，但地址来源、失败分类、默认次数和生命周期仍必须由各模块独立定义。

## 决策（Decision）

### 1. Framework HTTP 固定为 UnityWebRequest

- Framework HTTP 请求固定使用 `UnityWebRequestTransport` 和系统 DNS。
- 删除 BestHTTP 适配包、内部 BestHTTP/TLS 依赖、Framework DoH 管理器、IP 注入扩展点、传输注册 SPI、对应 Inspector 配置与旧专属遥测。
- UnityWebRequest 只使用请求总超时；不伪造独立连接超时。
- 每个物理请求独立获得完整超时时间，不定义整条候选链的共享总超时。
- WebSocket 保持独立实现，但不接受 Framework DoH/IP 注入。

### 2. 共享执行机制，各模块独立配置

Core 提供 `HttpFallbackPlanner`、`HttpFallbackExecutionPlan`、`HttpFallbackExecutionCursor` 与 `HttpFallbackPreferenceStore`，供 App、Asset、Network 复用。共享层只负责：

- 完整 URL 去重；
- 最近成功域名优先排序；
- `重试周期 → 完整轮次 → 候选` 的稳定坐标；
- 成功、可继续失败、取消和耗尽状态。

共享层不判断业务成功，不替模块决定哪些 HTTP 状态可继续，也不直接发起网络请求。

去重候选数为 `C`、完整轮数为 `R`、重试次数为 `K` 时：

```text
最大物理发送数 = C × R × (K + 1)
```

主备全部走一遍算一轮；全部轮数耗尽后才消耗一次重试。每次重试重新执行所有轮次和候选。

### 3. 三个模块的独立配置

| 模块 | 轮数配置 | 重试配置 | 默认值 | 最近成功 | 埋点开关 |
|---|---|---|---|---|---|
| App 版本检查 | `VersionCheckFallbackRoundCount` | `RetryRequestCount` | `1 / 1` | 默认开启 | `EnableUWRTracks`，默认开启 |
| Asset 热更新 | `FallbackRoundCount` | `RetryDownloadCount` | `1 / 3` | 默认开启 | `EnableUWRTracks`，默认开启 |
| HostKey + NetCmd | `BusinessFallbackRoundCount` | `RetryRequestCount` | `1 / 1` | 默认开启 | `EnableUWRTracks`，默认开启 |

Asset 的 `VersionsCheckWhiteList.json` 请求不复用普通热更新配置，单独使用 `StartupWhitelistFallbackRoundCount`、`StartupWhitelistRetryRequestCount`、`StartupWhitelistPreferLastSuccessfulHost`、`StartupWhitelistEnableUWRTracks` 与 `StartupWhitelistCheckTimeout`，默认值为 `1 / 1 / true / true / 5`。

普通单 URL `Nova.Network.GetAsync/PostAsync`、上传与调用方自行提供的单 URL 下载保持单 URL 语义，不猜测备用地址。

App、Asset 与 Network Inspector 必须就这些配置直接说明：一轮会走完全部有效且去重后的候选；重试次数不包含首次执行；超时属于每次物理请求，不是整链总超时；最近成功偏好只在当前进程内调整新计划的候选顺序，不会删除其他候选；`EnableUWRTracks` 仅控制埋点上报，不影响请求或下载。HTTP 状态是否继续下一候选仍按本 ADR 中三个模块各自的规则说明，不写成一套模糊的全局规则。

### 4. App 版本检查规则

- 传输失败、客户端数据处理失败、空正文、无效 JSON/版本规则、HTTP `404`、`408`、`429` 与 `5xx`：继续下一候选。
- 其他正式 HTTP `4xx`：停止整链并按既有宽容语义返回 `NoDownload`。
- 得到有效版本规则后停止；最近成功偏好只在取得有效规则时更新。
- `AppManager.DownloadAsync()` 仍是既有 APK 占位能力，本决策不扩展其实现。

### 5. Asset 热更新规则

- Asset 复用共享 Core，但由 `AssetDownloadUrlPolicy` 适配 YooAsset 的 `IDownloadUrlPolicy` 与 `IDownloadRetryPolicy`；不修改 YooAsset 源码，不关闭按需下载。
- 每个文件独立冻结完整候选计划，并发文件互不推进；具体语义见 [[ADR-080-asset-package-sticky-fallback-routing|ADR-080]]。
- 无响应、内容校验失败、HTTP `404`、`408`、`416`、`429` 与 `5xx` 继续；`401`、`403` 及其他 `4xx` 停止。
- 启动白名单、`.version`、`.hash/.bytes` 和 Bundle 保持各自现有超时来源与离线回退边界。

### 6. HostKey + NetCmd 业务协议规则

- `NetworkHostKeys.Value` 与 `FallbackValue` 生成主备完整 URL；请求体和请求头在首次发送前冻结，并在整条链中复用。
- 成功响应更新按 HostKey 隔离的最近成功域名。
- 任意正式 HTTP 响应，包括 `4xx/5xx`，都会结束候选链并交给既有业务响应解析；只有未取得正式 HTTP 响应的传输失败才继续。
- 服务端允许重复请求，因此不新增 operation-id 或客户端幂等协议。若请求可能已送达但未收到响应，`HttpResponse.DeliveryState` 保守标记为 `Unknown`。

### 7. 统一 UWR 埋点

- 事件固定为 `uwr_request_start`、`uwr_request_error`、`uwr_request_end`，schema 固定为 `1`。
- 一条可完整观测的逻辑链遵守 `1 start → 0～N error → 1 end`；主备、轮次和重试都在同一个 `uwr_chain_id` 下。
- 字段只保留 UWR 能稳定提供或框架能确定计算的值，完整 29 项定义以 `Assets/Framework/Tracks/Tracks.xlsx` 为准。
- Asset 额外使用 `uwr_download_operation_id`、`uwr_package`、`uwr_file_type` 聚合下载；WebGL HostPlayMode 的 WebNetwork 内容校验重试可能在同一 download operation 下产生新的 UWR chain。
- SDK 尚未就绪时采用有界内存队列，SDK 就绪后按顺序派发；单个 `ITrackPlugin` 异常不得阻断其他插件和网络请求。

### 8. TGA 上游 DoH 边界

Framework 去除 DoH 只约束 Nova 自己的 App、Asset、Network 与业务协议链。`com.solotopia.nova.framework.sdk.tga` 所包含的 ThinkingAnalytics 上游 SDK 网络实现不在本次替换范围内，其自带 DoH 能力保持原样；Framework 不接管、不删除，也不把它纳入 `uwr_*` 主备链。

### 9. 消费项目升级迁移

- Framework Editor 在编辑器与 Package Manager 稳定后自动从 manifest 移除当前及历史 BestHTTP/TLS direct package 与 testables，并触发一次 UPM Resolve；`packages-lock.json` 与 Package Manager 工程镜像由 Unity 重建，不直接改写。
- 旧 adapter 存在时，由 `versionDefines` 临时启用仅供首轮编译的旧 ABI 桥梁；注册为空操作且不参与请求，Resolve 移除 adapter 后桥梁自动退出编译，运行时始终只有 UWR。
- 旧 adapter 首轮编译必须由桥梁提供它已编译依赖的公开全名 `NovaFramework.Runtime.IHttpTransport` 及旧方法签名。新运行时内部契约因此命名为 `IUwrHttpTransport`，使两套 ABI 只在自动迁移窗口内可以同时编译。该命名是迁移兼容隔离，不表示 Framework 重新开放了多传输实现 SPI；强行恢复新契约的 `IHttpTransport` 名称会导致同名类型冲突，或要求扩大条件编译与 UWR 兼容面。
- 迁移器不改写业务代码、资源、HybridCLR、link、宏、registry scope 或 `Library`；TGA/ThinkingAnalytics 上游 DoH 明确排除。

## 后果（Consequences）

### 正面

- Nova 自有 HTTP 行为统一到 Unity 原生传输与系统 DNS，移除 BestHTTP/DoH 的双实现分叉。
- 三个模块共享经过测试的请求计划状态机，避免分别实现轮次、重试、取消和并发语义时发生漂移。
- 配置、HTTP 失败规则和下载生命周期仍由所属模块控制，不把 App、Asset 与业务协议强行揉成同一业务层。
- `uwr_*` 埋点能关联逻辑链并还原每次物理发送坐标。

### 代价与限制

- 不再支持 Framework DoH、指定连接 IP、BestHTTP 独立连接超时和旧专属诊断字段。
- `C × R × (K + 1)` 会放大最坏耗时；轮数、重试数和物理超时必须按模块审慎配置。
- 单 URL API 和未接入 HostKey + NetCmd 的第三方 SDK 不会自动获得主备能力。
- 主备域名必须由项目保证接口、鉴权、数据一致性与 TLS 证书能力等价。
- Editor loopback 与模拟传输测试不能替代 Android/iOS 弱网、DNS 故障、主 CDN 不可达和真实 CDN/CORS 验证。

## 被排除方案（Rejected Alternatives）

| 方案 | 否决理由 |
|---|---|
| 保留 BestHTTP + DoH 可选后端 | 线上联通表现不符合预期，且继续制造双传输、双配置与双遥测语义 |
| 在 UWR 上复刻 DoH IP 直连 | 会重新引入 Host、TLS SNI、证书校验和 NAT64 风险 |
| 三个模块各写一套候选算法 | 容易在轮次、重试、取消和并发语义上继续漂移 |
| 用一套全局配置覆盖三个模块 | App、Asset 与业务协议的默认值、地址来源和失败规则不同 |
| 为可能重放的业务请求新增 operation-id | 服务端已明确接受重复请求，不增加新的协议概念 |
| 自动为单 URL 请求推断备用域名 | 公共 API 没有可靠配对信息，可能产生不可控重放 |

## 验证依据（Verification）

- Unity 6000.4.2f1、Android active target 下刷新并编译完成，Console 编译错误为 0。
- Unity 6000.4.2f1 独立旧项目夹具以 `framework.besthttp@0.1.8`、Best HTTP 3.0.20 与 Best TLS 启动：旧 adapter 首轮编译通过，随后自动移除三包并使 manifest、lock 收敛；再次启动无重复改动。
- `LegacyNetworkPackageMigrationTests` 与 `UnityWebRequestTransportTests` 定向 EditMode 回归 `29/29` 通过；App、Asset、Network 三个 Inspector 实际加载时 Console 无 Error。
- App、Asset、共享 Planner 与真实 UWR loopback 等 7 个定向 EditMode 测试类最终通过 `121/121`。
- 用例覆盖候选公式、完整 URL 去重、最近成功优先、取消止链、断连重放、HTTP 503 业务止链及 Asset 独立文件计划。
- `Assets/Framework/Tracks/Tracks.xlsx` 中 29 个 `uwr_*` 属性与代码键集合一致。
- 活跃源码、`Packages` 与 `UPMPackages` 不再包含 Nova BestHTTP/DoH 实现；历史 CHANGELOG、负向依赖测试以及 TGA 上游实现不作为残留删除。
- `git diff --check HEAD` 通过；未修改 `Library` 与 YooAsset 源码。

## 当前事实来源（Sources）

- `Assets/Framework/Scripts/Runtime/Core/HttpFallback/`
- `Assets/Framework/Scripts/Runtime/Modules/Network/Managers/HttpManager/`
- `Assets/Framework/Scripts/Runtime/Modules/App/Managers/AppManager/`
- `Assets/Framework/Scripts/Runtime/Modules/Asset/Managers/AssetManager/`
- `Assets/Framework/Tracks/Tracks.xlsx`
- `Assets/Framework/Docs/Runtime/Modules/Network/HttpManager/HttpManager.md`
- `Assets/Framework/Docs/Runtime/Modules/Asset/AssetManager/Implements/AssetManager.md`
