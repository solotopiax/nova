# AdMobAdPlugin

## 1. 简介

`AdMobAdPlugin` 是独立 AdMob 渠道适配层，位于 `NovaFramework.SDK.AdMobAdPlugin.Runtime` 命名空间：

- 继承 `AdChannelPluginBase`
- 通过 `[AdChannel(typeof(AdMobAdChannelConfig))]` 绑定配置类型
- 声明渠道身份；在匹配官方 SDK 已安装的 Android / iOS Player 中执行 UMP、SDK 初始化并注册四格式广告位

当前代码已实现 Rewarded、Interstitial、Banner、AppOpen 的原厂加载、展示、收入回调与 Ready 查询，业务仍使用父广告聚合接口。本包 `package.json` 未直接安装 Google Mobile Ads SDK；Runtime / Editor asmdef 在官方 `com.google.ads.mobile` 的 `[11.5.0,12.0.0)` 范围启用适配。未安装匹配 SDK 或处于 Editor / 非移动平台时渠道报告初始化失败，不自动请求测试广告。源码具备实现不代表 Unity 编译、原生构建或设备广告效果已验收。

## 2. 当前成员与可见性

| 成员 | 可见性 | 说明 |
|---|---|---|
| `Name => "AdMobAdPlugin"` | public override | 渠道插件名称 |
| `Channel => AdChannelType.AdMob` | public override | 渠道类型标识 |
| `InitChannelSDKAsync(IAdChannelConfig config, CancellationToken ct)` | protected override | 执行 UMP 与必要表单，检查 `CanRequestAds()`，等待 `MobileAds.Initialize` 返回状态后注册四格式真实 ID；由基类初始化模板调用，业务不直接调用 |
| `WaitForPrivacyFlowAsync(CancellationToken ct)` | public override | 等待本轮 UMP 流程结束；取消只结束本次等待，不撤销原生表单，完成不证明用户同意 |
| `OnRequestAsync` / `OnShowAsync` / `OnIsReady` | protected override | 实现四格式原厂加载、全屏展示与真实 Ready 查询，由基类公开广告接口调用 |
| `ShowBanner()` / `HideBanner()` / `DestroyBanner()` | public override | 控制配置首个 Banner ID 对应的原厂对象；其他 Banner ID 的加载不改变控制目标 |
| `UpdateBannerPosition(BannerPosition)` / `UpdateBannerPosition(Vector2)` | public override | 更新首个 Banner 的停靠或逻辑像素位置，未创建时保存供后续创建 |
| `DisposeChannelSDKAsync(CancellationToken ct)` | protected override | 失效本轮并收回本渠道四格式对象，由基类释放模板调用 |

## 3. 运行时行为

- `AdPlugin` 从 `AdPluginConfig.ChannelConfigs.Items` 读取渠道配置，并按启用配置的 `PluginType` 反射创建实例；`Enabled=false` 的配置会被跳过。
- 目标平台须有有效 App ID，并已安装匹配官方 SDK。初始化将 `TagForUnderAgeOfConsent` 配置传给 UMP，依次等待 UMP 更新/必要表单与 SDK 状态，再将有效真实 ID 注册到 `AdChannelPluginBase`；加载和展示仍由业务的 `RequestAsync` / `ShowAsync` 驱动，不因填写配置而自动发广告请求。UMP 更新或表单失败会记录诊断，只有 UMP 当前 `CanRequestAds()` 为真才继续官方初始化。
- 没有有效广告位或广告尚未加载时，`IsReady(format)` 返回 `false`；全屏就绪查询结合原厂 `CanShowAd()` 和缓存有效期（Rewarded / Interstitial 一小时、AppOpen 四小时），Banner 查询已加载且未销毁的对象。聚合层其他渠道可以独立就绪，因此聚合插件的 Ready 状态不能用来证明 AdMob 已接入。
- 渠道类型和配置可被扫描、序列化；全屏组表达式由配置类的兼容真实 ID 访问器解析展开。当前初始化用这些快照调用 `RegisterAdUnits`，每个 ID 实际注册为独立单 ID 组：请求时各 ID 可以并行加载，各自失败后循环使用父包 `GroupRetryIntervalsSeconds`，成功后各自归零。没有调用 `RegisterAdGroups` 保留原组拓扑，因此 `A1,A2` 不会形成 A1 失败后才加载 A2 的组内串行 waterfall；表达式无效时该格式快照为空，不注册广告位。
- 本包已有 `AdMobAdPluginBuildProcessor`，在 Nova Android / iOS 预处理链从激活 `AdPluginConfig` 的独立 AdMob 配置写入项目 `Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset` 的目标 App ID，后续仍由官方处理器维护 Manifest / plist 和依赖；无效 App ID、重复启用配置或缺匹配 SDK 会阻断构建。MAX 包的 `MaxAdPluginBuildProcessor` 仍只读取 `MaxAdChannelConfig`，不会读取独立 `AdMobAdChannelConfig` 的 App ID。
- `WaitForPrivacyFlowAsync(ct)` 等待 UMP 初始化期流程结束；取消或失败清理也会结束该任务，完成不证明用户同意。官方状态无法映射为明确的通用个性化授权 bool，`IsUserConsentSet()` / `HasUserConsent()` 保持基类不支持查询的默认结果。
- 广告回调按当前对象与生命周期过滤并进入主线程处理；加载完成时失效的原厂对象会直接销毁，不能向本轮发布结果。全屏打开时发布展示成功，官方奖励回调只记录资格，关闭时才结算奖励并完成展示等待；展示失败会明确结算，不等待关闭。页面取消等待不撤销已发起的原生展示。
- 原厂收益回调的微单位金额仅在有效非负 USD 时除以 `1,000,000` 后发布到共享收益接口；非 USD 或无效金额只记录诊断，不自动换汇。收益金额不决定业务奖励资格。真实 UMP、填充、展示、奖励和收入仍须设备验收。
- Banner 使用固定 `AdSize.Banner`，创建时隐藏，业务显式显示后保留可见期望；显示、隐藏、销毁和位置只控制配置首个 Banner ID。销毁后需再次 `RequestAsync` 创建。宽度、自适应高度、背景色等接口仍继承基类默认结果，不视为本渠道已实现。
- 每个渠道实例只承担一轮生命周期。释放准确注销订阅、取消展示及加载观察等待并销毁本渠道四格式对象，不重置原厂全局隐私状态，也不清理其他使用者；重复释放等待同一次清理。已释放的 `AdPlugin` / `AdMobAdPlugin` 不重新初始化，SDKManager 完整释放后，新的初始化周期创建新的聚合插件和渠道实例。

## 4. 使用示例

构建预处理写入的官方 Settings 是项目中的正式持久资产，必要时会创建目录和资产，不在构建失败后自动还原。目标平台 App ID、原厂广告位 ID 和用户同意是不同输入；`TagForUnderAgeOfConsent` 只是年龄标志。Banner 的 Vector2 位置按逻辑像素取整，拒绝 NaN/Infinity，位置会保留供下一次创建；原厂 SetPosition 失败不回滚已保存的位置期望。全屏过期在读取 Ready 时清理，没有后台过期计时器。

```csharp
// 在 ConfigWindow 的广告聚合渠道配置列表里添加 AdMobAdChannelConfig，
// 保存 ConfigMaster 并正式导出对应坐标的 ConfigRuntime。
// 显式安装匹配官方 SDK 后，AdPlugin 按 PluginType 创建启用的 AdMobAdPlugin 并驱动初始化。
// 业务仍先 RequestAsync 并核对结果/IsReady，再展示；配置导出不等于设备广告可播。
```

业务广告调用仍通过父包 `AdPlugin` / `IAdPlugin`。当前独立 AdMob 配置不能替代 MAX 配置中的 AdMob App ID，也不能作为 MAX 原厂 Google 适配器的广告位配置入口。

## 5. 关联

- 配置类型：[AdMobAdChannelConfig.md](./AdMobAdChannelConfig.md)
- 渠道基类：[AdChannelPluginBase.md](../../../com.solotopia.nova.framework.sdk.ad/Nova/Doc/AdChannelPluginBase.md)
- MAX 构建处理器：[MaxAdPluginBuildProcessor.md](../../../com.solotopia.nova.framework.sdk.max/Nova/Doc/MaxAdPluginBuildProcessor.md)
