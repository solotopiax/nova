# Nova Framework - SDK - MAX 文档索引

> 本包为 Nova 框架 MAX 广告聚合插件，提供广告展示服务。
> 基于 `sdk.ad` 父包的 `AdChannelPluginBase` 抽象，实现 MAX 渠道的 Rewarded / Interstitial / Banner / AppOpen 广告类型调度。
> 第三方 MAX SDK 由官方 UPM 包 `com.applovin.mediation.ads` 提供，mediation adapters 由本包 `package.json` 统一声明；本包只保留 Nova 适配层与构建处理器，不再内置 `Core/MaxSdk`。

---

## 业务侧公开 API

| 类型 | 说明 | 文档 |
|---|---|---|
| `MaxAdPlugin` | MAX 广告聚合插件，实现 `AdChannelPluginBase`，封装 MAX SDK 初始化与广告加载/展示逻辑 | [MaxAdPlugin.md](./MaxAdPlugin.md) |
| `MaxAdChannelConfig` | MAX 渠道配置，包含 SDK Key、构建注入项、全屏广告组、Banner ID 列表与调试开关 | [MaxAdPlugin.md](./MaxAdPlugin.md) |
| `MaxAdGroupConfigValidator` | 公共纯配置校验器：组语法、真实 ID 唯一性和跨格式冲突 | [MaxAdPlugin.md](./MaxAdPlugin.md#广告组配置与原厂边界) |
| `MaxAdPluginBuildProcessor` | Editor 构建设置注入、Android ProGuard、iOS 5 项 Embed 查找名单，以及离线枚举网络后联网补齐 SKAdNetwork | [MaxAdPluginBuildProcessor.md](./MaxAdPluginBuildProcessor.md) |

业务从 `Nova.SDK.Get<IAdPlugin>()` 获取当前聚合接口；MAX 渠道由聚合层创建，不是该查询直接返回的实例。每个 MAX 渠道实例只初始化一次；旧聚合和渠道释放后不可复用，SDKManager 的新初始化周期会创建新聚合及渠道，业务需释放旧事件订阅并重新获取、订阅新实例。共享释放和初始化取消边界见[主文档](./MaxAdPlugin.md#共享释放与新生命周期)。实际原厂 Show 与 30 回调日志、实际渠道请求及展示后补量（`AutoRefill`）和自动重试（`Retry`）的新轮日志读法见[主文档](./MaxAdPlugin.md#本地调用与回调日志)；构建处理器当前不调用组校验器。

iOS 的两项 MAX adapter 原生依赖归属修复与 SKAdNetwork 补齐随包自动执行，复用现有 Player 构建流程；SKAdNetwork 的网络清单在本地枚举，ID 清单仍依赖官方 HTTP 接口。构建前配置检查、失败恢复和平台验收边界见[构建处理器文档](./MaxAdPluginBuildProcessor.md#构建前广告配置边界)与[iOS 原生依赖归属](./MaxAdPluginBuildProcessor.md#ios-原生依赖归属)。

源码与文档说明不代表 Player 构建、原生编译或真机广告已经通过，目标工程须按[验证边界](./MaxAdPlugin.md#验证边界)执行。

## 相关

- [MaxAdPlugin.md](./MaxAdPlugin.md) — MAX 渠道插件
- [MaxAdPluginBuildProcessor.md](./MaxAdPluginBuildProcessor.md) — 构建处理器
- [README](../../README.md#安装) — 安装、固定依赖和 registry
- [Ad 聚合接口](../../../com.solotopia.nova.framework.sdk.ad/Nova/Doc/IAdPlugin.md) — 业务入口
- [MAX 官方 Unity 接入文档](https://support.axon.ai/en/max/unity/overview/integration/) — 官方 SDK 的当前 documentationUrl
