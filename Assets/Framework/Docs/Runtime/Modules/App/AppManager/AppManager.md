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
- 缓存 `m_Config`
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

### 4. `DownloadAsync()` 仍是骨架

当前 `DownloadAsync(ct)` 仍直接抛出 `NotImplementedException`。

本次版本检查重试机制不改变 `PrimaryDownloadUrl`、`FallbackDownloadUrl` 或 APK 下载路径；APK 下载仍不因这些轮次、偏好或埋点开关而获得新回退行为。

### 5. `OpenStoreAsync()` 只负责打开商店 URL

它会：

1. 解析当前平台商店地址
2. 校验 URL 非空
3. `await Util.AppStore.OpenAsync(url)`

失败时返回 `false`，不会向上抛异常。

## 高价值状态

- `MatchedRule`
- `TargetStoreUrl`
- `TargetDownloadUrl`

## 风险点 / 易错点

- `MatchedRule / TargetStoreUrl / TargetDownloadUrl` 只在命中规则时更新；在 `NoDownload` 或异常降级路径里会被清空，避免残留旧值。
- `DownloadRoute == Apk` 不代表一定可下载；当前下载实现还没打通。
- 默认 `VersionCheckFallbackRoundCount=1`、`RetryRequestCount=1`。主备均配置且一直可重试失败时，默认最多会发送 4 次，不是 2 次。
- 远端 JSON 版本号和 `Application.version` 都需要满足 `System.Version` 格式；非法格式会记 warning，并按“不命中更新”处理。
- 推荐提示间隔缺失、为 `0` 或负数时保持原行为，每次命中推荐规则都提示；本地时间回拨时也会重新提示，避免形成无限期抑制。

## 继续阅读

关键源码：

- [AppManager.cs](../../../../../Scripts/Runtime/Modules/App/Managers/AppManager/Implements/AppManager.cs)
- [AppManager.Methods.cs](../../../../../Scripts/Runtime/Modules/App/Managers/AppManager/Implements/AppManager.Methods.cs)
- [AppManager.Download.cs](../../../../../Scripts/Runtime/Modules/App/Managers/AppManager/Implements/AppManager.Download.cs)

相关文档：

- [IAppManager.md](IAppManager.md)
- [AppManagerBase.md](AppManagerBase.md)
- [../Definitions/AppManagerConfig.md](../Definitions/AppManagerConfig.md)
- [../Definitions/AppVersionResult.md](../Definitions/AppVersionResult.md)
- [../Definitions/AppVersionResponse.md](../Definitions/AppVersionResponse.md)
