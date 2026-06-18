# AppManagerConfig

`AppManagerConfig` 是启动期 App 大版本检查配置 DTO。

它把 `AppComponent` Inspector 上的输入打包成 `AppManager` 可直接消费的一份配置。

## 配置语义

### 1. 版本检查输入

- `AppDownloadCheckUrl`
- `AppDownloadCheckUrlFallback`
- `TimeoutSeconds`

含义：

- `AppDownloadCheckUrl` 指向当前 DevelopMode 选中的主规则 JSON 地址
- `AppDownloadCheckUrlFallback` 指向当前 DevelopMode 选中的备用规则 JSON 地址
- `TimeoutSeconds` 是 GET 请求超时时长，当前默认值为 `5`
- 版本检查固定先尝试主地址；主地址为空、请求失败、超时或返回空内容时切到备用地址
- 主备都不可用时，本轮 App 大版本检查直接降级为 `NoDownload`，继续后续启动流程

### 2. 更新下载输入

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

### 3. 规则开关

- `UseRecommendedDownloadRule`
- `UseForcedDownloadRule`

匹配优先级固定为：

- 强制更新规则 > 推荐更新规则

## 风险点 / 易错点

- 这份配置只决定大版本检查与下载提示，不负责资源补丁检查。
- `AppDownloadCheckUrl` / `AppDownloadCheckUrlFallback` 已经是 `AppComponent.Start()` 选好的“当前模式生效值”，不是四组原始 Inspector 字段本身。

## 继续阅读

关键源码：

- [AppManagerConfig.cs](../../../../../Scripts/Runtime/Modules/App/Definitions/AppManagerConfig.cs)

相关文档：

- [../AppComponent.md](../AppComponent.md)
- [../AppManager/AppManager.md](../AppManager/AppManager.md)
- [AppDownloadRoute.md](AppDownloadRoute.md)
