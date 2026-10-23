# AdMobAdChannelConfig

## 1. 简介

`AdMobAdChannelConfig` 是 AdMob 渠道配置对象，实现 `IAdChannelConfig`，位于 `NovaFramework.SDK.AdMobAdPlugin.Runtime` 命名空间。在 `AdPluginConfig.ChannelConfigs.Items` 中以 `[SerializeReference]` 方式存储。

## 2. 配置字段

以下名称是业务侧的只读属性；实际编辑通过 ConfigWindow 保存对应的私有序列化字段。

| 属性 | 类型 / 默认值 | 说明 |
|---|---|---|
| `Enabled` | `bool` / `true` | 是否启用该渠道配置；关闭后聚合层不创建该渠道实例 |
| `AppIdAndroid` | `string` / 未设置 | Android 平台 App ID，格式 `ca-app-pub-XXXXXXXXXXXXXXXX~XXXXXXXXXX` |
| `AppIdIOS` | `string` / 未设置 | iOS 平台 App ID，格式同上 |
| `TagForUnderAgeOfConsent` | `bool` / `false` | 是否将用户标记为低于隐私同意年龄；传入官方 UMP 请求，按项目年龄策略配置 |
| `RVPlacementGroups` | `IReadOnlyList<string>` / 空列表 | 激励视频广告组原始表达式 |
| `InterPlacementGroups` | `IReadOnlyList<string>` / 空列表 | 插屏广告组原始表达式 |
| `BannerPlacementIds` | `IReadOnlyList<string>` / 空列表 | Banner 真实广告 ID 列表；每项只填写一个 ID，不支持广告组表达式 |
| `AppOpenPlacementGroups` | `IReadOnlyList<string>` / 空列表 | 开屏广告组原始表达式 |

App ID 与广告位 ID 用途不同；全屏组项填写后台真实广告位 ID，不填写 App ID。

## 3. 契约行为

- `Channel` 固定返回 `AdChannelType.AdMob`。
- `PluginType` 固定返回 `typeof(AdMobAdPlugin)`，由聚合层反射创建实例。
- 全屏源列表为空或 `null` 表示对应广告格式未配置；只有实际注册且加载成功的广告位才可能就绪。

三种全屏列表每项一组，英文 `,` 与 `|` 是同义组内分隔符，可以混用。例如列表 `["A1,A2,A3", "B1|B2"]` 表示两组；Inspector 的两个项分别填写 `A1,A2,A3` 与 `B1|B2`，不把引号、方括号或整个 JSON 数组填入某一项。示例 ID 仅用于说明结构，需替换成后台真实广告位 ID。

共享 `AdGroupConfigParser` 会 Trim 并过滤空表达式及空 ID。相同格式内，组内或跨组重复 ID 都是错误；分号、中文分隔符与 JSON 结构也是错误，不自动去重或猜测语法。解析结果保留诊断，消费者必须先检查 `IsValid` 才能注册当前格式。

旧 `RVPlacementIds`、`InterPlacementIds`、`AppOpenPlacementIds` 属性保留，每次通过共享解析器返回按组及组内顺序展开的真实 ID 深只读快照；当前格式任一表达式无效时返回空列表。`*PlacementGroups` 返回原始表达式的不可写视图，不缓存解析结果；配置后续编辑可以改变后续读取，运行计划须另行物化独立快照。`BannerPlacementIds` 保留原来的单 ID 列表接口。

序列化字段分别使用 `FormerlySerializedAs("m_RVPlacementIds")`、`FormerlySerializedAs("m_InterPlacementIds")` 与 `FormerlySerializedAs("m_AppOpenPlacementIds")`。旧单 ID 列表自然成为多个单 ID 组，不需要增加选择模式或改写成 JSON。

父包通过 `RegisterAdGroups` 保留完整组拓扑时，调度固定为组间并行、组内串行，没有广告组选择模式。单 ID 失败推进本组后项；只有整组本轮全部失败才推进统一 `AdPluginConfig.ChannelConfigs.GroupRetryIntervalsSeconds` 间隔下标，各组独立计数。本组任意 ID 加载成功后归零；间隔到末项后循环回首项，默认 `2、4、8、16、32、2……` 秒。空序列或任何非有限正数使整个序列回退默认值。本类没有独立的重试间隔字段。

当前 AdMob 初始化读取兼容 `RVPlacementIds`、`InterPlacementIds`、`AppOpenPlacementIds` 快照，再调用 `RegisterAdUnits`；每个展开后的真实 ID 注册为独立单 ID 组。因此请求时各 ID 可以并行加载，各自失败后循环使用上述共享间隔，成功后各自归零；原表达式的组内顺序只保留在展开快照中，不形成组内串行 waterfall。例如 `A1,A2` 当前会成为两个独立单 ID 组，不能理解为 A1 失败后才加载 A2。某格式表达式无效时，兼容快照为空，该格式不会注册广告位。

## 4. 当前限制

`AdMobAdPlugin` 已实现 UMP、原厂 SDK 初始化、四格式加载/展示/真实 Ready 查询；其运行前提是 Android / iOS Player、有效目标 App ID、已安装匹配官方 SDK 和 UMP 当前允许请求广告。`TagForUnderAgeOfConsent` 只是 UMP 请求参数，不代替项目年龄判断，也不直接设置或证明用户授权。本包 `package.json` 未声明官方 SDK 直接依赖，Runtime / Editor asmdef 仅在官方 `com.google.ads.mobile` 的 `[11.5.0,12.0.0)` 范围启用适配。配置可序列化、扫描和解析，不代表这些平台条件或设备广告链已通过验证。

本包 `AdMobAdPluginBuildProcessor` 从激活的 ConfigRuntime 读取本类目标平台的 `AppIdAndroid/IOS`，在 Nova Android / iOS 预处理链写入项目 `Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset`，保留官方 Manifest / plist 和依赖处理链。未启用独立 AdMob 时不执行注入；启用时，重复配置、无效 App ID、缺少匹配 SDK 或官方 Settings 类型/字段不匹配会阻断构建。MAX 包的 `MaxAdPluginBuildProcessor` 仍读取 `MaxAdChannelConfig.AdMobAppIdAndroid/IOS` 并写入 `AppLovinSettings`，不读取本类 App ID。

业务先通过父包 `RequestAsync` 等待加载并核对结果 / `IsReady`，再展示；ConfigWindow 保存、正式导出和构建 App ID 注入均不能替代真机验收。配置由本轮聚合插件创建的新渠道实例消费，同一渠道释放后不复用；SDKManager 完整释放后，新的初始化周期创建新的聚合插件和渠道实例。

## 5. 关联

- 渠道插件：[AdMobAdPlugin.md](./AdMobAdPlugin.md)
- 共享广告聚合配置：[AdPluginConfig.md](../../../com.solotopia.nova.framework.sdk.ad/Nova/Doc/AdPluginConfig.md)
- 公共组解析与运行契约：[AdChannelPluginBase.md](../../../com.solotopia.nova.framework.sdk.ad/Nova/Doc/AdChannelPluginBase.md)
- MAX 构建注入：[MaxAdPluginBuildProcessor.md](../../../com.solotopia.nova.framework.sdk.max/Nova/Doc/MaxAdPluginBuildProcessor.md)
