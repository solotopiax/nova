# Nova Framework - SDK - AdMob 文档索引

> 本包提供独立 Google AdMob 配置与渠道适配层，基于父包 `com.solotopia.nova.framework.sdk.ad` 的 `AdChannelPluginBase` 建立类型与配置对应关系，复用聚合公开接口。
> 当前源码已接入 UMP、官方 SDK 初始化、四格式广告位注册与加载/展示/真实 Ready 查询；仅安装匹配官方 SDK 后在 Android / iOS Player 执行。这些源码事实不代表已完成 Unity 编译、原生构建或真机广告验收。

## 业务侧公开 API

| 类型 | 说明 | 文档 |
|---|---|---|
| `AdMobAdPlugin` | 独立 AdMob 渠道，声明 `Channel=AdMob`；源码实现 UMP、官方初始化和 Rewarded / Interstitial / Banner / AppOpen 适配 | [AdMobAdPlugin.md](./AdMobAdPlugin.md) |
| `AdMobAdChannelConfig` | AdMob 配置，承载 Android / iOS App ID、Rewarded / Inter / AppOpen 广告组及 Banner 单 ID 列表 | [AdMobAdChannelConfig.md](./AdMobAdChannelConfig.md) |

## 当前状态

- `InitChannelSDKAsync(IAdChannelConfig, CancellationToken)` 是受保护的初始化钩子，先执行 UMP 更新及必要表单，再检查 `CanRequestAds()`，等待 `MobileAds.Initialize` 返回状态后调用 `RegisterAdUnits(...)` 注册四格式真实 ID；业务不直接调用。未安装匹配 SDK 或处于 Editor / 非移动平台时报告渠道初始化失败，不自动请求测试广告。
- `AdPlugin` 按 `IAdChannelConfig.PluginType` 创建启用的 AdMob 渠道；初始化注册后，加载使用原厂四格式 API，全屏 Ready 核对 `CanShowAd()` 与缓存有效期，Banner 核对当前已加载对象。没有有效广告位或未完成加载时仍不能提供就绪广告，聚合其他渠道的 Ready 不证明 AdMob 可播。
- 配置可被序列化、扫描和解析；ConfigWindow 保存、导出或聚合 SDK 生命周期就绪不等于原厂 AdMob SDK 初始化或广告可播。UMP 流程完成也不等于用户明确允许个性化广告，通用授权 bool 查询仍不支持；真实 UMP、填充、展示、奖励与收入需另取设备和厂商证据。
- 当前包 `package.json` 没有 Google Mobile Ads SDK 直接依赖，须通过既有安装流程显式接入官方 `com.google.ads.mobile`；Runtime / Editor asmdef 在 `[11.5.0,12.0.0)` 定义 `NOVA_GOOGLE_MOBILE_ADS`。本包已有 `AdMobAdPluginBuildProcessor`，从激活配置写入项目官方 `GoogleMobileAdsSettings.asset` 的目标 App ID，保留官方原生处理链；未安装匹配 SDK、App ID 无效或重复启用配置时阻断构建。MAX 包中的 AdMob App ID 注入仍读取 MAX 配置，不读取本包配置。
- 全屏配置仍遵循共享解析规则：List 每项一组，项内英文逗号与竖线同义；完整组契约为组间并行、组内串行，无广告组选择模式。当前 AdMob 初始化读取兼容 `*PlacementIds` 快照，将表达式展开后调用 `RegisterAdUnits`，每个真实 ID 注册为独立单 ID 组，各 ID 可以并行加载并各自使用共享循环间隔。没有调用 `RegisterAdGroups` 保留原组拓扑，因此不能把 MAX 的组内串行行为套到本渠道；某格式表达式无效时该格式快照为空，不注册广告位。
- `TagForUnderAgeOfConsent` 默认 `false`，按项目年龄策略传入 UMP，不代表用户授权；标准 Banner 的显示、隐藏、销毁与位置只控制配置首个 ID。每个渠道实例只承担一轮生命周期，释放准确退订并收回本渠道原厂对象；重复释放共享同次结果，释放后的聚合和渠道不重新初始化，SDKManager 完整释放后，新的初始化周期创建新的聚合插件和渠道实例。

## 相关

- [AdMobAdPlugin.md](./AdMobAdPlugin.md) — AdMob 渠道适配与运行边界
- [AdMobAdChannelConfig.md](./AdMobAdChannelConfig.md) — AdMob 渠道配置与兼容访问器
- [AdPluginConfig.md](../../../com.solotopia.nova.framework.sdk.ad/Nova/Doc/AdPluginConfig.md) — 广告聚合配置与共享重试间隔
- [AdChannelPluginBase.md](../../../com.solotopia.nova.framework.sdk.ad/Nova/Doc/AdChannelPluginBase.md) — 已注册渠道的公共运行时契约
- [MaxAdPluginBuildProcessor.md](../../../com.solotopia.nova.framework.sdk.max/Nova/Doc/MaxAdPluginBuildProcessor.md) — MAX 配置的 AdMob App ID 构建注入
- [README.md](../../README.md) — 安装、依赖声明与当前能力边界
