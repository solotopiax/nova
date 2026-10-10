# Nova Framework - SDK - MAX

> 包名：`com.solotopia.nova.framework.sdk.max`
> 当前版本：`0.1.13`

MAX 广告聚合插件，提供广告展示服务。

本包只包含 Nova MAX 适配层，第三方 MAX SDK 由官方 UPM 包 `com.applovin.mediation.ads@8.6.4` 提供，不再内置 `Core/MaxSdk`。MAX mediation adapters 也由本包 `package.json` 统一声明为 AppLovin 官方 UPM 依赖。

## 安装

通过 Nova 私域 UPM 注册表以 UPM 依赖形式接入（注册表地址向 Nova Framework 内部开发人员索取）：

```json
"dependencies": {
  "com.solotopia.nova.framework.sdk.max": "0.1.13"
}
```

## 当前行为

- RV、Inter、Banner、AppOpen 均支持配置多个广告位 ID；同一格式下的空闲广告位并行加载，展示时优先选择已就绪且收益最高的广告位。
- 插屏 displayed 回调中的 `Success`、`RewardGranted` 均为 true；激励关闭时的奖励资格取 MAX 官方奖励回调状态，开屏保持 false。业务实际发奖由调用方处理。
- 激励、插屏和开屏的 `nova_ad_show`、`nova_ad_show_result`、`nova_ad_hidden`、`ad_ilrd`、`ad_impression` 关联同次展示的 `nova_ad_impression_id`；关闭时没有匹配上下文则不附加 ID。
- 收益打点插件在使用时获取并缓存：有值直接使用，空值在当前回调线程查询，仍不可用则逐目标 Warning 并跳过本次派发；下一次事件再尝试，不等待初始化或排队补发。
- 非 Banner 每次收益回调分别派发 `ad_ilrd` 和 `ad_impression`；MAX 全部 Banner 广告位共享 ILRD 批次，默认累计 5 次派发一次，`ad_impression` 仍逐次派发。
- Banner 控制 API 使用配置列表中的首个 ID。`ShowBanner()` 同时启动 MAX 自动刷新，`HideBanner()` 同时停止自动刷新。
- `MaxAdChannelConfig.BannerAutoRefreshIntervalSeconds` 配置 Banner 自动刷新间隔，默认 `10` 秒，可配置范围为 `5–120` 秒。
- Banner native view 按广告位幂等创建；加载失败后若业务仍要求显示，后续加载成功会恢复展示。
- 当前适配层不设置 `adaptive_banner`；开启自动刷新前会通过 `ad_refresh_seconds` extra parameter 写入面板配置的刷新间隔。
- 当前源码也未设置禁用 MAX SDK 自动重试或禁用非 Banner B2B 广告位参数，这两项不属于现版本已接入能力。
- MAX 初始化完成时缓存用户广告隐私授权状态，并通过广告公共接口 `WaitForPrivacyFlowAsync()`、`IsUserConsentSet()` 与 `HasUserConsent()` 暴露；等待接口会在初始化期 CMP 未展示或用户完成同意/拒绝后结束。

## 文档

- [Nova/Doc/INDEX.md](./Nova/Doc/INDEX.md)
- [Nova/Doc/MaxAdPlugin.md](./Nova/Doc/MaxAdPlugin.md)
- [奖励资格、回调与埋点的对应关系](./Nova/Doc/MaxAdPlugin.md#回调与埋点对照)
- [Nova/Doc/MaxAdPluginBuildProcessor.md](./Nova/Doc/MaxAdPluginBuildProcessor.md)

## 维护

变更记录见 [CHANGELOG.md](./CHANGELOG.md)。每次发版必须在 CHANGELOG 中追加对应版本节，否则发布脚本会中断。

## 当前开源状态

- 当前结论：不应直接进入公开仓；公开版只应保留 Nova 适配层与接入文档，不应继续提交 AppLovin MAX 插件本体。

## 许可与第三方声明

- 包根许可边界说明见 [LICENSE.md](./LICENSE.md)。
- 上游来源、第三方声明与当前再分发边界见 [THIRD_PARTY_NOTICES.md](./THIRD_PARTY_NOTICES.md)。
- AppLovin SDK 本体、adapter、资源与上游声明由官方 UPM 包提供。
