# Nova Framework - SDK - AdMob

> 包名：`com.solotopia.nova.framework.sdk.admob`
> 当前版本：`0.0.5`

本包提供独立 Google AdMob 渠道配置与运行时适配，供 Nova 广告聚合插件创建和托管。当前源码已实现 UMP 隐私流程、Google Mobile Ads SDK 初始化，以及 Rewarded、Interstitial、Banner、AppOpen 的原厂加载、展示、收入回调与真实 Ready 查询；业务仍通过父包 `AdPlugin` / `IAdPlugin` 调用。

这些实现只在安装匹配官方 SDK 的 Android / iOS Player 中执行，不能据此认定 Unity 编译、原生构建或真机广告已经验收。

## 安装

通过 Nova 私域 UPM 注册表以 UPM 依赖形式接入（注册表地址向 Nova Framework 内部开发人员索取）：

```json
{
  "dependencies": {
    "com.solotopia.nova.framework.sdk.admob": "0.0.5"
  }
}
```

当前 `package.json` 声明的直接依赖如下：

| 包 | 声明版本 |
|---|---|
| `com.solotopia.nova.framework` | `0.7.0` |
| `com.solotopia.nova.framework.sdk.ad` | `1.2.0` |

父包 `sdk.ad@1.2.0` 另声明 Framework `0.7.0` 依赖；当前本包与 Nova 广告包的最低 Unity 版本统一为 `6000.6.7f1`。当前依赖声明不包含独立 Google Mobile Ads SDK，项目须通过既有安装流程显式接入官方 `com.google.ads.mobile`。Runtime / Editor asmdef 在版本范围 `[11.5.0,12.0.0)` 定义 `NOVA_GOOGLE_MOBILE_ADS`；未安装匹配 SDK 或处于 Editor / 非移动平台时，渠道报告初始化失败，不自动请求测试广告。

接入时在 ConfigWindow 的广告聚合渠道列表中保留一项启用的 `AdMobAdChannelConfig`，填写目标平台 App ID、所需格式的真实广告位 ID 与 `TagForUnderAgeOfConsent` 年龄标记，保存 ConfigMaster 并正式导出当前坐标的 ConfigRuntime。App ID 使用 `~`，广告位 ID 使用 `/`，两者不可混填；配置保存或导出成功不等于原厂初始化或广告可播。

## 广告组配置

本包使用 `sdk.ad` 的共享广告组解析器。`AdMobAdChannelConfig` 的 `RVPlacementGroups`、`InterPlacementGroups`、`AppOpenPlacementGroups` 每个列表项表示一组，项内用英文逗号 `,` 或竖线 `|` 分隔真实广告 ID。例如列表 `["A1,A2", "B1|B2"]` 表示两组；引号与方括号仅用于说明列表结构，Inspector 每项分别填写 `A1,A2` 和 `B1|B2`。示例 ID 需要替换成广告后台的真实 ID。

旧的单 ID 列表自然成为多个单 ID 组。空列表表示未配置对应格式；解析时 Trim 并过滤空项，同一格式内重复 ID、分号、中文逗号、顿号及 JSON 结构会报错，不会自动去重或猜测语法。`BannerPlacementIds` 仍是每项一个真实 ID 的列表，不支持分组表达式。

旧 `RVPlacementIds`、`InterPlacementIds`、`AppOpenPlacementIds` 访问器保留，每次按当前组顺序解析并返回展开后的真实 ID 快照；表达式无效时返回空列表。Unity 旧序列化字段通过 `FormerlySerializedAs` 迁移，新的 `*PlacementGroups` 访问器提供原始表达式的不可写视图。

父包的完整广告组调度契约为组间并行、组内串行，没有广告组选择模式。组内单个 ID 失败只推进后项；整组本轮全部失败后才使用 `AdPluginConfig.ChannelConfigs.GroupRetryIntervalsSeconds` 等待。全部渠道与格式共用同一间隔配置，各组独立维护下标；本组任意 ID 加载成功后归零，到末项后循环回首项。默认等待为 `2、4、8、16、32、2……` 秒；空列表或任意非有限正数使整个序列回退默认值。

当前 `AdMobAdPlugin` 读取兼容 `*PlacementIds` 快照，将全屏表达式展开后调用 `RegisterAdUnits`，每个真实 ID 实际注册为独立单 ID 组。业务请求时，各 ID 可以并行加载，各自失败后按共享间隔循环重试；原始表达式的组内串行拓扑没有保留，不能把上述完整组契约当作本渠道已经实现的 waterfall。详细契约见 [AdMobAdChannelConfig](./Nova/Doc/AdMobAdChannelConfig.md)，共享间隔见 [AdPluginConfig](../com.solotopia.nova.framework.sdk.ad/Nova/Doc/AdPluginConfig.md)。

本包已有 `AdMobAdPluginBuildProcessor`，从激活的 ConfigRuntime 中读取启用的独立 AdMob 配置，在 Nova Android / iOS 预处理链把目标 App ID 写入项目 `Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset`，后续 Manifest / plist 与依赖仍由官方处理链维护。启用独立 AdMob 时，缺少匹配 SDK、无效 App ID、重复启用配置或官方 Settings 类型/字段不匹配会阻断构建。MAX 包的 [MaxAdPluginBuildProcessor](../com.solotopia.nova.framework.sdk.max/Nova/Doc/MaxAdPluginBuildProcessor.md) 另读取 `MaxAdChannelConfig.AdMobAppIdAndroid/IOS` 并写入 `AppLovinSettings`，不读取本包配置。

渠道每个实例只承担一轮生命周期；释放时注销准确订阅、取消本轮等待并销毁本渠道原厂广告对象，重复释放复用同一次清理结果，不重置原厂全局隐私状态。同一 `AdPlugin` / `AdMobAdPlugin` 释放后不重新初始化，SDKManager 完整释放后，新的初始化周期创建新的聚合插件和渠道实例。UMP 流程结束不证明用户同意个性化广告，真实填充、展示、奖励与收入仍须设备和厂商证据。

## 维护

详细入口见 [包内文档索引](./Nova/Doc/INDEX.md)，变更记录见 [CHANGELOG.md](./CHANGELOG.md)。日常变更写入 `[Unreleased]`；正式发版时生成对应版本节，发布脚本会检查变更记录。
