# AdPlugin

> 包名：`com.solotopia.nova.framework.sdk.ad`
> 当前版本：`1.1.11`

广告聚合插件基类，支持 RV / Inter / Banner / AppOpen / InGameDisplay，内置多渠道调度、广告位状态机、重试与打点。

## 安装

通过 Nova 私域 UPM 注册表以 UPM 依赖形式接入（注册表地址向 Nova Framework 内部开发人员索取）：

```json
"dependencies": {
  "com.solotopia.nova.framework.sdk.ad": "1.1.11"
}
```

## 当前行为

- `AdPlugin.RequestAsync` 并行请求所有已启用渠道；每个渠道内部并行请求该格式下全部空闲广告位，任一广告位加载成功即可完成本次请求。
- `ShowCompleted` 表示渠道 SDK 已回调 displayed，不代表广告已关闭或激励已完成；非 Banner 在关闭或展示失败后自动续杯。
- 加载、展示、关闭和收益等业务事件统一回到 Unity 主线程；状态推进、批次结算和不依赖 Unity API 的打点可在 SDK 原始回调线程完成。
- `IAdPlugin.GetCountryCodeAsync(...)` 统一提供广告国家码；默认等待 5 秒，超时后读取广告模块上次成功缓存，空值或 `IV` 返回空字符串。
- `IAdPlugin.IsUserConsentSet()` 与 `HasUserConsent()` 提供广告隐私授权查询；必须组合判断，以区分“尚未设置”和“明确拒绝”。
- `IAdPlugin.WaitForPrivacyFlowAsync(...)` 供业务启动页等待广告初始化期间的隐私流程；无弹窗时正常完成，有弹窗时在用户同意或拒绝后完成。

## 文档

- [Nova/Doc/INDEX.md](./Nova/Doc/INDEX.md)
- [Nova/Doc/IAdPlugin.md](./Nova/Doc/IAdPlugin.md)
- [Nova/Doc/AdChannelPluginBase.md](./Nova/Doc/AdChannelPluginBase.md)

## 维护

变更记录见 [CHANGELOG.md](./CHANGELOG.md)。每次发版必须在 CHANGELOG 中追加对应版本节，否则发布脚本会中断。
