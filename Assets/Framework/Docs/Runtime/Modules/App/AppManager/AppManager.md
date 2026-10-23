# AppManager

`AppManager` 是启动期 App 大版本检查与下载提示路由的真实实现层。

它负责三件事：

- 请求 CDN 规则 JSON 并解析结果
- 按 `DownloadRoute` 计算当前流程真正需要的目标商店地址或 APK 主下载地址
- 向流程层暴露本次检查的命中状态

## 核心流程

### 1. Initialize：接管配置并绑定 `IHttpManager`

`Initialize(config)` 会：

- 校验 `config != null`
- 从 `FrameworkManagersGroup` 获取 `IHttpManager`
- 取消未提交的旧 APK 下载，缓存 `m_Config` 并清空旧规则命中状态
- 清理旧 Manager 生命周期留下的 App 版本检查最近成功域名偏好

### 2. CheckAsync：总开关关闭或无 URL 时直接跳过

如果 `EnableAppUpdate == false`：

- 不发起 HTTP 请求
- 清空上一次规则命中状态
- 直接返回 `AppVersionResult.NoDownload`

如果主、备版本检查地址都为空：

- 直接 warning
- 返回 `AppVersionResult.NoDownload`

### 3. 真正检查链：共享候选计划 -> HTTP GET -> JSON -> 结果

`AppManager` 只复用 Core 的候选去重、完整轮次、额外完整重试周期、游标和最近成功偏好存储；它自己保留 App 版本规则的失败分类，不能把 Asset 或业务协议的语义带进来。

去重后的候选数为 `C`、`VersionCheckFallbackRoundCount` 为 `R`、`RetryRequestCount` 为 `K` 时，最多物理 GET 次数为：

```text
C × R × (K + 1)
```

顺序为“首次执行/重试 → 完整轮次 → 当前候选”。主备齐全且 `R=1`、`K=1` 时为 `主 → 备 → 主 → 备`；因此 `RetryRequestCount=1` 表示失败后再执行一次完整主备计划，不是只把主或备重发一次。每次物理请求均使用完整的 `TimeoutSeconds`，没有共享链总超时。

一次候选完成后的处理如下：

| 响应分类 | 后续动作 |
|---|---|
| 未取得 HTTP 响应的传输失败、客户端数据处理失败 | 推进下一个候选 |
| 正式 HTTP `404` / `408` / `429` / `5xx` | 推进下一个候选 |
| 其他正式 HTTP 状态（例如 `401`） | 停止整链并返回 `NoDownload` |
| HTTP 成功但 body 为空、JSON 解析失败或版本规则无效 | 推进下一个候选 |
| 合法版本规则 JSON（包括最终为 `NoDownload`） | 停止整链，按规则结果返回 |

内置 `HttpManager` 实现 `IPhysicalHttpManager`，因此 App 可以把调用方取消令牌传到实际 UWR：取消会中止当前物理请求并停止游标，不会进入备用域名或后续重试。已有自定义 `IHttpManager` 不实现该内部入口时仍可调用旧 `GetAsync`，但无法获得内置 UWR 的物理取消和 App 链路埋点能力。

最近成功偏好以 `app.version_check` 作用域只保存在当前进程：仅当候选返回有效版本规则时标记成功。候选全部耗尽不会删除旧偏好；只有新成功、当前配置不再包含旧端点，或 Manager 初始化/关闭重置时才会改变它。

`EnableUWRTracks` 开启时，App 为整条版本检查链发出统一 `uwr_request_start → 0..N uwr_request_error → uwr_request_end`。每个物理发送只归这条链一次；空正文和无效规则会以 `invalid_response` 终态及稳定叶子错误码标识，而不会误记为 HTTP 成功。

`ParseVersionResult(...)` 当前读取两个版本阈值和一个推荐提示间隔：

- `ForcedDownloadVersion`
- `RecommendedDownloadVersion`
- `RecommendedDownloadPromptIntervalSeconds`

命中规则是：

- `UseForcedDownloadRule == true` 且 `ForcedDownloadVersion > Application.version`
  - `m_MatchedRule = Forced`
  - 按 `DownloadRoute` 只解析当前需要的目标地址
  - 返回 `ForcedDownload`
- `UseRecommendedDownloadRule == true` 且 `RecommendedDownloadVersion > Application.version`
  - 如果距离上次主动放弃推荐更新尚未达到有效提示间隔：返回 `NoDownload`
  - 否则令 `m_MatchedRule = Recommended`
  - 按 `DownloadRoute` 只解析当前需要的目标地址
  - 返回 `RecommendedDownload`
- 其他情况
  - 返回 `NoDownload`

优先级固定为：

- `ForcedDownload` > `RecommendedDownload`

推荐更新提示间隔的状态使用启动期 `PlatformPlayerPrefs` 保存，原因是版本检查发生在业务 `Persist.LoadAsync()` 之前。只有用户在推荐更新弹窗中主动取消时才通过 `RecordRecommendedDownloadDismissed()` 写入 UTC Unix 秒并立即落盘；确认更新、强制更新和检查失败都不会写入。存储异常只记 warning，不得阻断当前启动；最坏结果是下次启动再次提示。

地址解析规则是：

- `DownloadRoute == Store`：只检查当前平台商店地址
- `DownloadRoute == Apk`：只检查 `PrimaryDownloadUrl`
- `FallbackDownloadUrl`：当前启动期版本检查不校验，也不会作为 `TargetDownloadUrl` 的回退值

### 4. `DownloadAsync()` 下载并提交本地 APK 文件

公开签名保持 `UniTask<string> DownloadAsync(CancellationToken)`。内置实现只在 Android Player 执行，要求已初始化、`EnableAppUpdate=true`、`DownloadRoute=Apk`，且当前命中强更或推荐更新。Editor、iOS、WebGL 会抛 `PlatformNotSupportedException`，不写文件。

每次调用冻结 `TargetDownloadUrl` 与可选 `FallbackDownloadUrl`，按规范完整 URL 去重、主地址在前。只允许无内嵌凭据和片段的绝对 HTTPS，保留 Unity 默认 TLS 证书校验，最多跟随五次 HTTPS 重定向。每个候选最多尝试一次，网络、响应或归档结构失败才推进备用；本地文件 IO 故障停止，取消不推进备用。

响应通过 `DownloadHandlerFile` 直接写入 `Path.Hotfix.ApkDownloadedFullPath` 同目录的唯一 `.part` 文件，不把 APK 全量放入内存。每个候选含重定向最多 30 分钟，连续 60 秒无新字节会中止，单文件上限 1 GiB；仅接受 HTTP 200 与 identity 内容编码，存在 `Content-Length` 时必须与实际文件长度一致。这些限制是下载器内部固定值，不使用版本规则 GET 的轮次、重试、偏好、埋点或 `TimeoutSeconds`。

完成后检查文件非空、大小上限，以及 ZIP 中唯一非空的 `AndroidManifest.xml`；不提取归档，结构检查不代表签名有效、目标版本正确或可以安装。合格文件通过同目录 `File.Move` / `File.Replace` 提交为 `${Application.persistentDataPath}/update.apk`，返回该绝对路径。失败或取消保留此前正式文件并删除本次临时件；同一 Manager 拒绝并发下载，重新初始化与 `Shutdown()` 会取消未提交操作，提交前再次检查令牌。安装是独立 Native 能力或业务动作，不由下载入口隐式触发。

### 5. `OpenStoreAsync()` 只负责打开商店 URL

它会：

1. 解析当前平台商店地址
2. 校验 URL 非空
3. `await Util.AppStore.OpenAsync(url)`

正常日志处理条件下，空白地址或打开异常返回 `false`。当前 ct 未传给 AppStore，也不要求此前命中规则；true 仅表示已发起跳转，不代表商店可用。

## 高价值状态

- `MatchedRule`
- `TargetStoreUrl`
- `TargetDownloadUrl`

## 风险点 / 易错点

- CheckAsync 开始先清 MatchedRule/targets；合法 JSON 解析后也先清再匹配。并发检查共享这些字段，无独立代际或锁；不要用返回结果推导另一条在途检查的状态。
- `DownloadRoute == Apk` 仍需 Android Player、有效 HTTPS 地址及已命中的更新规则；下载路径只表示文件提交完成。
- 默认 `VersionCheckFallbackRoundCount=1`、`RetryRequestCount=1`。主备均配置且一直可重试失败时，默认最多会发送 4 次，不是 2 次。
- 远端 JSON 版本号和 `Application.version` 都需要满足 `System.Version` 格式；非法格式会记 warning，并按“不命中更新”处理。
- 推荐提示间隔缺失、为 `0` 或负数时保持原行为，每次命中推荐规则都提示；本地时间回拨时也会重新提示，避免形成无限期抑制。

## 参数和失败后的状态

Initialize 非空配置成功后保存 DTO 原引用，不深复制或热读 Inspector；获取 HTTP 失败时不会提交新配置，但 HTTP 依赖字段可能已被写空。重新初始化/Shutdown 取消 APK 下载而不拥有版本检查的独立 CTS；在途检查仍须由调用方令牌和时序管理。Priority=11 仅控制 Update/Shutdown，Update 不自动发检查。

版本规则要求两个阈值都能被 System.Version 解析，即使相应规则开关关闭也一样；这不是 SemVer，既不验证两个阈值先后也不验证规则与下载目标匹配。CompareVersions 两者都非法返回 0、单方非法返回 -1，不能把 -1 一律解释为远端更旧。命中目标为空或未知路由只记录日志，不撤销规则命中。IsValidUrl 仅非空白；Store iOS 取 AppStoreUrl，其余平台取 AndroidStoreUrl，不能由名称推导完整 URL 校验。

推荐放弃状态固定键为 `Nova.App.RecommendedDownloadDismissedAtUnixSeconds`，Invariant 解析并按 UTC 秒比较；只有 `now-dismissedAt < interval` 才抑制，等于间隔允许提示，无效旧时间或回拨不抑制。保存失败仅 Warning。

版本检查 R 最少 1、K 最少 0，端点偏好按规范域名而非完整 URL 对比；合法 NoDownload 也可标成功。每条链独立 cursor，HTTP 响应在 finally 回池，不得跨回调保留；旧 IHttpManager 仅发送前检查令牌，不提供中途物理取消。404/408/429/所有 >=500、无正式响应或 DataProcessingError 可推进，其他正式失败停止。取消与异常外层均降级 NoDownload，不是服务端“无需更新”的证明。

UWR 埋点只在支持物理接口且开关开启时发，operation=`app_version_check`、scene=`app`。链 ID 为每次新建；attemptsStarted 计真正准备发送的次数，request/total 耗时为毫秒，TimeoutSeconds 为秒；telemetry send index 为 0-based 且饱和到 int，日志显示的轮次/发送序号加 1，重试周期仍原值。primary/fallback/other 只作标签，空正文与无效规则叶子码分别 empty_response/invalid_version_rule；上报不改变路由，也不证明远端接收，tracker/日志异常仍可中断处理。

APK 内部下载器还要求 1..2 个候选、单实例独占；内部构造器的传输委托替身不改变真实路径、校验与提交。目标规范为绝对文件路径并拒绝现存祖先/目标的链接，父目录创建可先发生。每候选以 CreateNew 创建唯一 part，只有 InvalidDataException 推进备用，取消/本地 IO 和其他异常停止；finally 删除 part 的失败可能覆盖原异常。没有回滚已提交正式文件的 Cancel。

Content-Length 缺失为未知 -1，非法值失败而非忽略；下载关闭后按 ZIP 结构验证不超过 100000 条目且唯一非空 AndroidManifest.xml。ZipArchive 先解析目录，条目数检查不是硬内存上限；结构通过不校验包名/versionCode、签名或目标检查版本。

接口与 Base 的抽象声明没有这些实现体；外部自定义管理器及物理取消、文件下载支持需单独确认。

## 继续阅读

关键源码：

- [AppManager.cs](../../../../../Scripts/Runtime/Modules/App/Managers/AppManager/Implements/AppManager.cs)
- [AppManager.Methods.cs](../../../../../Scripts/Runtime/Modules/App/Managers/AppManager/Implements/AppManager.Methods.cs)
- [AppManager.Download.cs](../../../../../Scripts/Runtime/Modules/App/Managers/AppManager/Implements/AppManager.Download.cs)
- [AppApkDownloader.cs](../../../../../Scripts/Runtime/Modules/App/Managers/AppManager/Implements/AppApkDownloader.cs)

相关文档：

- [IAppManager.md](IAppManager.md)
- [AppManagerBase.md](AppManagerBase.md)
- [../Definitions/AppManagerConfig.md](../Definitions/AppManagerConfig.md)
- [../Definitions/AppVersionResult.md](../Definitions/AppVersionResult.md)
- [../Definitions/AppVersionResponse.md](../Definitions/AppVersionResponse.md)
