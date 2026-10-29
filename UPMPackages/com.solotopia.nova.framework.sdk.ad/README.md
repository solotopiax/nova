# AdPlugin

> 包名：`com.solotopia.nova.framework.sdk.ad`
> 当前版本：`1.2.2`

广告聚合插件基类，调度 Rewarded / Interstitial / Banner / AppOpen / Native 格式（具体支持由渠道实现决定，当前 MAX 支持前四种），内置多渠道调度、广告位状态机、重试与打点。

## 安装

通过 Nova 私域 UPM 注册表以 UPM 依赖形式接入（注册表地址向 Nova Framework 内部开发人员索取）：

```json
"dependencies": {
  "com.solotopia.nova.framework.sdk.ad": "1.2.2"
}
```

## 当前行为

- AdPlugin.RequestAsync 并行请求当前已登记渠道；基类默认组间并行、组内串行，失败尝试下一 ID，成功停止本组。组末项失败后按 ad 统一间隔序列循环等待再从首 ID 开始，成功后该组下标归零；本轮全部被观察组失败返回失败，后台继续重试。
- `ShowCompleted` 表示渠道 SDK 已回调 displayed，不代表广告已关闭或激励已完成；非 Banner 在关闭或展示失败后自动续杯。
- `AdResult.RewardGranted` 由渠道设置奖励资格；MAX 插屏展示成功时即为 true，激励关闭时取官方奖励回调状态，开屏为 false。关闭入口不会按广告收益回调改写该字段。
- `RaiseAdClosed` 保留原有签名，并提供带渠道附加属性的重载；MAX 全屏关闭埋点因此携带本次展示的 `nova_ad_impression_id`。
- 基类渠道的加载、展示、关闭和收益等业务事件统一回到 Unity 主线程；组状态、批次结算与 SDK Load / Ready / Show 统一主线程，纯收益打点保留 SDK 原线程即时上报。
- `IAdPlugin.GetCountryCodeAsync(...)` 统一提供广告国家码；默认等待 5 秒，超时或返回空值 / `IV` 时返回空字符串，不再在广告模块内持久化缓存。
- `IAdPlugin.IsUserConsentSet()` 与 `HasUserConsent()` 提供广告隐私授权查询；必须组合判断，以区分“尚未设置”和“明确拒绝”。
- IAdPlugin.WaitForPrivacyFlowAsync(...) 供业务启动页等待广告初始化期间的隐私流程；MAX / AdMob 在不适用或流程结束时完成，失败和释放也会结算各自等待。完成不等于已授权或广告已就绪，自定义渠道需实现同等流程边界。

聚合插件初始化完成只表示渠道列表与桥接已创建；Events.InitResult 转发单个渠道的初始化结果，不能当作全部渠道成功的汇总。实际可用性以 RequestAsync 结果及 IsReady 为准。

正常生命周期由 SDKManager 每轮创建新聚合和新渠道；已释放 AdPlugin 不支持同对象再初始化，重复释放共享同一次清理结果。SDKManager 完整释放后开启新初始化轮时，业务需重新获取新 IAdPlugin / AdPlugin 并重订阅；重复等待组件缓存的 InitializeTask 不会重启 SDK。初始化 ct 仅关联尚未结束的渠道准备与初始化，成功后不长期控制该渠道的事件与业务等待。

## 广告组配置

MAX的RVPlacementGroups、InterPlacementGroups、AppOpenPlacementGroups保持字符串List，每项是一组，项内用英文`,`或`|`分隔。`["A1,A2,A3", "B1"]`先请求A1和B1；旧`["A1","A2"]`是两个单ID组。统一等待配置在`ChannelConfigs.GroupRetryIntervalsSeconds`，默认`[2,4,8,16,32]`秒，只有整组失败才推进一次下标，到末尾循环；任意项成功归零，各组独立计时。没有加载模式选择。

基类渠道取消业务等待不取消原生加载或展示，全屏占用保持到真实关闭 / 展示失败或释放。原厂无回调时不自动超时切 ID。聚合 Request 自身能结束外层取消等待；Show 与隐私等待仅透传 ct，非基类自定义渠道须自行响应取消、结算释放等待并保证主线程事件和展示占用，详见[取消令牌边界](./Nova/Doc/AdPlugin.md#取消令牌的作用范围)。

当前独立 AdMob 已实现官方 SDK 的 Rewarded / Interstitial / Banner / AppOpen 和 UMP，需安装官方依赖并在 Android / iOS Player 验证。它将全屏组配置展开为真实 ID 后通过旧注册入口建立独立单 ID 组；MAX 则保留表达式的组内串行拓扑，不能把 MAX 示例的组内串行保证直接用于当前 AdMob。

## 文档

- [Nova/Doc/INDEX.md](./Nova/Doc/INDEX.md)
- [Nova/Doc/IAdPlugin.md](./Nova/Doc/IAdPlugin.md)
- [Nova/Doc/AdChannelPluginBase.md](./Nova/Doc/AdChannelPluginBase.md)
- [奖励资格与回调时点](./Nova/Doc/AdPluginEvents.md#奖励资格与回调时点)

## 维护

变更记录见 [CHANGELOG.md](./CHANGELOG.md)。日常变更写入 CHANGELOG 的 `[Unreleased]`，发版时由发布流程归入版本节；已发布历史保持不变。
