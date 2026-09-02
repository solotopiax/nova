# Nova Framework - SDK - MAX

> 包名：`com.solotopia.nova.framework.sdk.max`
> 当前版本：`0.1.8`

MAX 广告聚合插件，提供广告展示服务。

本包只包含 Nova MAX 适配层，第三方 MAX SDK 由官方 UPM 包 `com.applovin.mediation.ads@8.6.4` 提供，不再内置 `Core/MaxSdk`。MAX mediation adapters 也由本包 `package.json` 统一声明为 AppLovin 官方 UPM 依赖。

## 安装

通过 Nova 私域 UPM 注册表以 UPM 依赖形式接入（注册表地址向 Nova Framework 内部开发人员索取）：

```json
"dependencies": {
  "com.solotopia.nova.framework.sdk.max": "0.1.8"
}
```

## 当前行为

- RV、Inter、Banner、AppOpen 均支持配置多个广告位 ID；同一格式下的空闲广告位并行加载，展示时优先选择已就绪且收益最高的广告位。
- Banner 控制 API 使用配置列表中的首个 ID。`ShowBanner()` 同时启动 MAX 自动刷新，`HideBanner()` 同时停止自动刷新。
- `MaxAdChannelConfig.BannerAutoRefreshIntervalSeconds` 配置 Banner 自动刷新间隔，默认 `10` 秒，可配置范围为 `5–120` 秒。
- Banner native view 按广告位幂等创建；加载失败后若业务仍要求显示，后续加载成功会恢复展示。
- 当前适配层不设置 `adaptive_banner`；开启自动刷新前会通过 `ad_refresh_seconds` extra parameter 写入面板配置的刷新间隔。
- 当前源码也未设置禁用 MAX SDK 自动重试或禁用非 Banner B2B 广告位参数，这两项不属于现版本已接入能力。
- MAX 初始化完成时缓存用户广告隐私授权状态，并通过广告公共接口 `WaitForPrivacyFlowAsync()`、`IsUserConsentSet()` 与 `HasUserConsent()` 暴露；等待接口会在初始化期 CMP 未展示或用户完成同意/拒绝后结束。

## 文档

- [Nova/Doc/INDEX.md](./Nova/Doc/INDEX.md)
- [Nova/Doc/MaxAdPlugin.md](./Nova/Doc/MaxAdPlugin.md)
- [Nova/Doc/MaxAdPluginBuildProcessor.md](./Nova/Doc/MaxAdPluginBuildProcessor.md)

## 维护

变更记录见 [CHANGELOG.md](./CHANGELOG.md)。每次发版必须在 CHANGELOG 中追加对应版本节，否则发布脚本会中断。

## 当前开源状态

- 当前结论：不应直接进入公开仓；公开版只应保留 Nova 适配层与接入文档，不应继续提交 AppLovin MAX 插件本体。

## 许可与第三方声明

- 包根许可边界说明见 [LICENSE.md](./LICENSE.md)。
- 上游来源、第三方声明与当前再分发边界见 [THIRD_PARTY_NOTICES.md](./THIRD_PARTY_NOTICES.md)。
- AppLovin SDK 本体、adapter、资源与上游声明由官方 UPM 包提供。
