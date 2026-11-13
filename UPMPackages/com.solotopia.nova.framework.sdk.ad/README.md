# AdPlugin

> 包名：`com.solotopia.nova.framework.sdk.ad`
> 当前版本：`1.2.4`

广告聚合插件基类，调度 Rewarded / Interstitial / Banner / AppOpen / Native 格式（具体支持由渠道实现决定，当前 MAX 与独立 AdMob 支持前四种），内置多渠道调度、广告位状态机、重试与打点。

## 安装

通过 Nova 私域 UPM 注册表以 UPM 依赖形式接入（注册表地址向 Nova Framework 内部开发人员索取）：

```json
"dependencies": {
  "com.solotopia.nova.framework.sdk.ad": "1.2.4"
}
```

本包只提供聚合与渠道基类，不包含可独立出广告的原厂 SDK。项目组可在 Nova PlugPals 的内部云仓库安装本包及所选渠道包，需有对应内部 registry 的访问权限；操作入口见 [PlugPals](README.md#跨包文档定位)（`com.solotopia.nova.framework` 包内 `Docs/Editor/Windows/PlugPalsWindow.md`）。当前四格式渠道见 [MAX](README.md#跨包文档定位)（`com.solotopia.nova.framework.sdk.max` 包内 `README.md`） 与 [独立 AdMob](README.md#跨包文档定位)（`com.solotopia.nova.framework.sdk.admob` 包内 `README.md`），各自的官方外网依赖、安装条件与平台边界以渠道说明为准。`Native` 枚举存在不表示这两个渠道已实现原生广告展示。

## 当前行为

- Banner 首次显示重新选择当前就绪渠道，后台重试或取消等待后加载成功仍可显示。显示后固定实例归属，Hide 保留、Destroy 清除；具体类 `AdPlugin.TryShowBanner()` 可查询是否发出调用，无广告或显示失败记录 Warning，不代表曝光。详见 [Banner 控制](./Nova/Doc/AdPlugin.md)。

- 广告收益协议由 `AdChannelPluginBase.StartRevenueReport` 统一承接。配置 `AdPluginConfig.RevenueReportCmdName=AdRevenueReport` 后，MAX 在 `TrackMaxAdIlrd` 派发时显式调用，AdMob 在 `Paid` 入口显式接入；原始 `RaiseRevenue` / `RaiseRevenueImmediately` 仅用于业务通知。自定义渠道须同步接入协议入口。
- IAA 协议类型为 `PbIAAReportData`、`PbNetIAAReportReq` 和 `PbNetIAAReportResp`，请求通过 `ad_channel` 与 `iaa` 传递数据，由 `Services/AdRevenueReportNetService.cs` 统一校验、构造并调用 `NetService.SendAsync`。
- `RevenueReportCmdName` 为空时兼容旧资产并跳过自动上报；`RevenuePaid` 仍只是 Unity 主线程业务通知，不代表网络上报已成功。实际发送采用应用 AES 加密的 Protobuf POST，参数和网络结果日志均为 Debug，协议、失败边界和排查顺序见[收益上报说明](./Nova/Doc/INDEX.md#广告收益上报-proto-协议层)。
- MAX Banner 后台协议与 ad_ilrd 共用一份渠道累计及存档（默认 5 次），在同一次派发中使用相同金额和最后一次回调的广告位。全部打点目标不可用时保留累计，协议同步等待；网络失败只记录警告。AdMob 尚无分析 ad_ilrd，显式复用渠道累计。非 Banner 不受阈值影响，ad_impression 仍逐次派发，详见[收益累计说明](./Nova/Doc/AdChannelPluginBase.md#广告收益上报)。
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
- [Nova/Doc/AdPlugin.md](./Nova/Doc/AdPlugin.md)
- [Nova/Doc/AdPluginConfig.md](./Nova/Doc/AdPluginConfig.md)
- [奖励资格与回调时点](./Nova/Doc/AdPluginEvents.md#奖励资格与回调时点)

## 维护

变更记录见 [CHANGELOG.md](./CHANGELOG.md)。日常变更写入 CHANGELOG 的 `[Unreleased]`，发版时由发布流程归入版本节；已发布历史保持不变。

## 跨包文档定位

本页链接相对于当前文件；独立包之间不假定相邻安装。先用 `UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()` 按下表精确包名查找，以该包的 `resolvedPath` 为根读取文档入口，再按包内相对链接进入目标。无 Editor 通道时使用当前有效 manifest 或解析工具给出的实际包位置。

| 包名 | 包内文档入口 | 本文引用的包内位置 |
|---|---|---|
| `com.solotopia.nova.framework` | `Docs/INDEX.md` | `Docs/Editor/Windows/PlugPalsWindow.md`<br>`Docs/Runtime/Modules/Network/NetService.md`<br>`Docs/Runtime/Modules/SDK/Definitions/Data.md`<br>`Docs/Runtime/Modules/SDK/Definitions/ISDKPlugin.md`<br>`Docs/Runtime/Modules/SDK/Definitions/ISDKPluginConfig.md`<br>`Docs/Runtime/Modules/SDK/Events/ObservableEvent.md`<br>`Scripts/Runtime/Modules/SDK/Plugins/Ad/IAdPlugin.cs`<br>`Scripts/Runtime/Modules/SDK/Plugins/Ad/IBannerControl.cs` |
| `com.solotopia.nova.framework.sdk.ad` | `Nova/Doc/INDEX.md` | `Sample AdDemo / README.md#先登录再操作广告` |
| `com.solotopia.nova.framework.sdk.admob` | `Nova/Doc/INDEX.md` | `Nova/Doc/AdMobAdPlugin.md`<br>`README.md` |
| `com.solotopia.nova.framework.sdk.max` | `Nova/Doc/INDEX.md` | `Nova/Doc/MaxAdPlugin.md`<br>`Nova/Doc/MaxAdPlugin.md#本地调用与回调日志`<br>`Nova/Scripts/Runtime/MaxAdPlugin.RV.cs`<br>`README.md` |

`Sample 名称 / 相对路径` 表示该包导入的示例内容；通过 Package Manager 和实际导入清单定位当前副本，再从其 README 进入。不要把开发仓源目录或某个版本的导入目录当作固定位置。
