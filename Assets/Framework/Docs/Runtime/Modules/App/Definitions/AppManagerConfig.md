# AppManagerConfig

`AppManagerConfig` 是启动期 App 大版本检查配置 DTO。

它把 `AppComponent` Inspector 上的输入打包成 `AppManager` 可直接消费的一份配置。

## 配置语义

### 1. App 更新总开关

- `EnableAppUpdate`

默认值为 `false`。关闭时 `CheckAsync()` 不发起版本检查请求，直接返回 `NoDownload`；资源热更新仍由 Asset 模块的 `EnableHotfix` 独立控制。需要 App 大版本检查时，调用方应显式设为 `true`。

### 2. 版本检查输入

- `AppDownloadCheckUrl`
- `AppDownloadCheckUrlFallback`
- `TimeoutSeconds`
- `VersionCheckFallbackRoundCount`
- `RetryRequestCount`
- `PreferLastSuccessfulHost`
- `EnableUWRTracks`

含义：

- `AppDownloadCheckUrl` 指向当前 DevelopMode 选中的主规则 JSON 地址
- `AppDownloadCheckUrlFallback` 指向当前 DevelopMode 选中的备用规则 JSON 地址
- `TimeoutSeconds` 是**每次物理 GET**独享的超时时长，当前默认值为 `5`
- `VersionCheckFallbackRoundCount` 是一个执行周期内的完整候选轮数，最小为 `1`，默认 `1`
- `RetryRequestCount` 是首个周期耗尽后的额外完整执行周期数，最小为 `0`，默认 `1`
- `PreferLastSuccessfulHost` 默认 `true`：当前进程内最近返回有效版本规则的域名会排到下一条检查链每轮首位
- `EnableUWRTracks` 默认 `true`：控制 App 版本检查逻辑链的统一 UWR 埋点，不影响其他模块

设去重后候选数为 `C`、完整轮数为 `R`、重试次数为 `K`，最大物理请求数为：

```text
C × R × (K + 1)
```

顺序固定为“首次执行/重试 → 完整轮次 → 当前候选”。主、备齐全时，`R=1, K=1` 的顺序是 `主 → 备 → 主 → 备`；这保证主备每次完整执行都有机会，避免把“重试 1 次”误解成仅重发一个域名。

候选推进规则：传输/客户端数据处理失败、`404`、`408`、`429`、`5xx`、空正文、无效 JSON 或无效版本规则会继续；其他正式 HTTP 状态（例如 `401`）停止整链。合法 JSON 即使计算结果为 `NoDownload` 也会停止整链。调用方取消时，内置 UWR 会中止当前物理请求且不再推进候选。

最近成功偏好不落盘。整链失败不会清除它；只有新有效规则成功、配置候选不再包含旧域名，或 AppManager 初始化/关闭时才失效。

### 3. 更新下载输入

- `DownloadRoute`
- `PrimaryDownloadUrl`
- `FallbackDownloadUrl`
- `AndroidStoreUrl`
- `AppStoreUrl`

当前实现的消费规则是：

- `DownloadRoute == Store`
  - iOS 使用 `AppStoreUrl`
  - 其他平台使用 `AndroidStoreUrl`
  - 不检查 APK 下载地址
- `DownloadRoute == Apk`
  - 只检查并使用 `PrimaryDownloadUrl`
  - `FallbackDownloadUrl` 仍为 APK 下载链路的备用地址
  - 不检查商店地址

### 4. 规则开关

- `UseRecommendedDownloadRule`
- `UseForcedDownloadRule`

匹配优先级固定为：

- 强制更新规则 > 推荐更新规则

## 风险点 / 易错点

- 这份配置只决定大版本检查与下载提示，不负责资源补丁检查。
- `AppDownloadCheckUrl` / `AppDownloadCheckUrlFallback` 已经是 `AppComponent.Start()` 选好的“当前模式生效值”，不是四组原始 Inspector 字段本身。
- 这些轮次、重试、偏好和埋点字段只作用于 App 版本规则 GET；`PrimaryDownloadUrl` / `FallbackDownloadUrl` 的 APK 下载行为保持不变。

## 继续阅读

关键源码：

- [AppManagerConfig.cs](../../../../../Scripts/Runtime/Modules/App/Definitions/AppManagerConfig.cs)

相关文档：

- [../AppComponent.md](../AppComponent.md)
- [../AppManager/AppManager.md](../AppManager/AppManager.md)
- [AppDownloadRoute.md](AppDownloadRoute.md)
