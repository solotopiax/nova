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

### 2. CheckAsync：总开关关闭或无 URL 时直接跳过

如果 `EnableAppUpdate == false`：

- 不发起 HTTP 请求
- 清空上一次规则命中状态
- 直接返回 `AppVersionResult.NoDownload`

如果 `AppDownloadCheckUrl` 为空：

- 直接 warning
- 返回 `AppVersionResult.NoDownload`

### 3. 真正检查链：HTTP GET -> JSON -> 结果

主链是：

1. 先用 `m_HttpManager.GetAsync(url, timeout)` 请求主地址
2. 主地址请求失败、body 为空、JSON 无法解析或版本规则无效时，请求备用地址
3. 主备都不能得到有效规则时降级 `NoDownload`；有效规则再由 `ParseVersionResult(body)` 计算结果

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
