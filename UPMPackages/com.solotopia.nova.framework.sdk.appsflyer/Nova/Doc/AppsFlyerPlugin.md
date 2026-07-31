# AppsFlyerPlugin

**类签名**：`public sealed partial class AppsFlyerPlugin : SDKPluginBase, IAttributionPlugin`

**命名空间**：`NovaFramework.SDK.AppsFlyerPlugin.Runtime`

**程序集**：`NovaFramework.SDK.AppsFlyerPlugin.Runtime`

**全局访问**：`Nova.SDK.Get<AppsFlyerPlugin>()` / `Nova.SDK.Get<IAttributionPlugin>()`

`AppsFlyerPlugin` 封装 AppsFlyer 初始化、归因事件、转化数据、深度链接和 AppsFlyer ID 发布。第三方 `IAppsFlyerConversionData` 由运行时创建的 `AppsFlyerConversionListener` 实现，插件本身只实现 Nova 的 `IAttributionPlugin`。

## 配置与初始化

插件通过 `ConfigType => typeof(AppsFlyerPluginConfig)` 声明配置。配置在 ConfigMaster 中启用后，SDKManager 会从 `IConfigManager` 自动解析并注入；业务侧不再调用 `SetConfig`。

`Priority => 20`，在 `TGAPlugin（Priority = 10）` 完成初始化后进入下一分桶，确保初始化阶段可以读取 TGA 设备 ID 与访客 ID。

| 字段 | 用途 |
|---|---|
| `DevKey` | AppsFlyer Dev Key；为空时跳过初始化 |
| `AppId` | App Store Connect 中应用的 Apple ID；为空时跳过初始化 |
| `LogEnable` | AppsFlyer 调试日志开关 |
| `OneLinkHost` | Android App Link 与 iOS Associated Domains 使用的 Host |
| `OneLinkFallbackName` | Android/iOS 备用 URL scheme |
| `OneLinkPathPrefix` | Android OneLink pathPrefix |
| `ReportCmdName` | 登录后向业务服务器上报 AppsFlyer ID 的 NetCmd 名称 |

初始化顺序：

1. 缓存 `AppsFlyerPluginConfig`，校验 `SDKComponent`、`DevKey` 和 `AppId`。
2. 订阅 `SDKEventData.UserLogin`。
3. 创建 `AppsFlyerConversionListener`，调用 `AppsFlyer.initSDK`。
4. 通过 `ITrackPlugin.FetchDataAsync` 等待 `SDKDataKeys.TGADevicesId` 和 `SDKDataKeys.TGADistinctId`，向 Additional Data 写入 `ta_devices_id`、`ta_distinct_id` 和 `app_id`。
5. iOS 配置 ATT 等待后调用 `AppsFlyer.startSDK`。
6. 发布 `SDKDataKeys.AppsFlyerId`。

## 公开 API

### IAttributionPlugin

```csharp
void SetUserId(string userId);
void TrackEvent(TrackEvent evt);
void TrackEvent(string eventName, Dictionary<string, object> parameters);
UniTask<AttributionData> GetAttributionAsync(CancellationToken ct = default);
event Action<AttributionData> OnAttributionResolved;
```

`GetAttributionAsync` 在已有缓存时立即返回，否则等待平台回调；调用方应通过 `CancellationToken` 提供自己的超时策略。

### AppsFlyer 专有方法

```csharp
string GetAppsFlyerID();
Dictionary<string, object> GetConversionData();
Dictionary<string, object> GetDeepLinkData();
void EnableTCFDataCollection(bool shouldCollect);
```

`GetConversionData` 和 `GetDeepLinkData` 返回当前缓存；对应平台回调尚未到达时可以为 `null`。

## 使用示例

```csharp
await Nova.SDK.InitializeTask;

IAttributionPlugin attribution = Nova.SDK.Get<IAttributionPlugin>();
attribution.OnAttributionResolved += data =>
{
    Log.Debug($"campaign={data.Campaign}, mediaSource={data.MediaSource}");
};

attribution.TrackEvent("level_complete", new Dictionary<string, object>
{
    { "level", 10 },
});

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
AttributionData result = await attribution.GetAttributionAsync(cts.Token);
```

用户登录时调用 `Nova.SDK.Login(userId)`。插件收到统一登录事件后会调用 `AppsFlyer.setCustomerUserId`，并在 `ReportCmdName` 有效时把已发布的 AppsFlyer ID 上报到业务服务器。

## 构建处理

`AppsFlyerPluginBuildProcessor` 根据同一份运行时配置处理平台工程：

- Android：确保 `Assets/Plugins/Android/gradleTemplate.properties` 中存在 `android.uniquePackageNames=false`，并注入 OneLink intent-filter。
- iOS：注入 Associated Domains、备用 URL scheme、SKAdNetwork 归因端点和 application identifier。

## 边界

- AppsFlyer 包不直接依赖 TGA 包，但初始化时需要一个已可用的 `ITrackPlugin` 发布 TGA 设备 ID 和访客 ID；缺少该能力或数据未发布时，不会继续执行 `AppsFlyer.startSDK` 和 AppsFlyer ID 发布。
- 插件不实现 `ITrackPlugin`，通用归因事件入口是 `IAttributionPlugin.TrackEvent`。
- `AppsFlyerConversionListener` 是第三方 SDK 回调适配器，不是业务查询入口。
- 初始化异常由插件内部记录，不会继续向 SDKManager 抛出，因此不会阻断其他 SDK 插件；业务侧不能仅凭整体 SDK 初始化完成来判定 AppsFlyer SDK 已启动。

## 相关

- [INDEX.md](./INDEX.md)
- [README.md](../../README.md)
