# AppsFlyerPlugin

**类签名**：`public sealed partial class AppsFlyerPlugin : SDKPluginBase, IAttributionPlugin, ISDKLoginReceiver, ISDKLoginSessionEndReceiver, ISDKInitializationDependencies`

**命名空间**：`NovaFramework.SDK.AppsFlyerPlugin.Runtime`

**程序集**：`NovaFramework.SDK.AppsFlyerPlugin.Runtime`

**全局访问**：`Nova.SDK.Get<AppsFlyerPlugin>()` / `Nova.SDK.Get<IAttributionPlugin>()`

`AppsFlyerPlugin` 封装 AppsFlyer 初始化、归因事件、转化数据、深度链接和 AppsFlyer ID 发布。第三方 `IAppsFlyerConversionData` 由运行时创建的 `AppsFlyerConversionListener` 实现；插件通过 Nova 归因、账号和能力依赖接口接入框架。

## 配置与初始化

插件通过 `ConfigType => typeof(AppsFlyerPluginConfig)` 声明配置。配置在 ConfigMaster 中启用后，SDKManager 会从 `IConfigManager` 自动解析并注入；业务侧不再调用 `SetConfig`。

`Name => "AppsFlyer"`，`Priority => 20` 仅用于稳定广播/释放顺序，不决定初始化先后。`InitializationDependencies` 声明 `new SDKInitializationDependency(typeof(ITrackPlugin), true)`，Manager 先等待已启用的埋点能力提供者 Ready，插件随后等待其 TGA 设备 ID 与访客 ID 数据。

| 字段 | 用途 |
|---|---|
| `DevKey` | AppsFlyer Dev Key；Android 守卫拒绝空值，实际传入原厂 `initSDK` |
| `AppId` | App Store Connect 中应用的 Apple ID；iOS 守卫拒绝空值，实际传入原厂 `initSDK` 与 Additional Data |
| `LogEnable` | AppsFlyer 调试日志开关 |
| `OneLinkHost` | Android App Link 与 iOS Associated Domains 使用的 Host |
| `OneLinkFallbackName` | Android/iOS 备用 URL scheme |
| `OneLinkPathPrefix` | Android OneLink pathPrefix |
| `ReportCmdName` | 登录后向业务服务器上报 AppsFlyer ID 的 NetCmd 名称 |

初始化顺序：

1. Manager 按能力依赖等待 `ITrackPlugin`，注入 `AppsFlyerPluginConfig`；插件创建标识上报 Service，缓存配置并校验 SDK 场景入口和当前平台字段。
2. 设置调试日志，创建 `AppsFlyerConversionListener`，调用 `AppsFlyer.initSDK` 并开启 TCF 数据采集。
3. 通过 `ITrackPlugin.FetchDataAsync` 并发等待 `SDKDataKeys.TGADevicesId` 和 `SDKDataKeys.TGADistinctId`，向 Additional Data 写入 `ta_devices_id`、`ta_distinct_id` 和 `app_id`。
4. iOS 调用原厂 ATT 等待配置（60 秒），再调用 `AppsFlyer.startSDK`。
5. Editor 注入 mock 归因并发布 mock AppsFlyer ID；设备从原厂取得 ID 后发布 `SDKDataKeys.AppsFlyerId`。

Config 字段未设置业务默认物料；`LogEnable` 默认 false，OneLink 三项默认未配置。守卫失败或初始化异常会记录日志、销毁本次创建的 Listener 并继续抛给 Manager，目标插件状态为 Failed；失败隔离不会阻断其他独立插件。

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

### 生命周期公开入口

```csharp
void OnSDKLogin(string userId, long sessionId);
void OnSDKLoginSessionEnded(long sessionId);
IReadOnlyList<SDKInitializationDependency> InitializationDependencies { get; }
```

这些入口由 Manager 调用，不代替业务的统一 `Nova.SDK.Login` / `EndLoginSession`。登录写入当前 sessionId、同步原厂 Customer User ID，并异步上报已发布 AppsFlyer ID；等待结束后 sessionId 不再匹配时停止旧账号上报。结束会话更新会话标记，不清空原厂 Customer User ID 或归因缓存。

## 使用示例

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
if (await Nova.SDK.WaitForPluginAsync<AppsFlyerPlugin>(cts.Token)
    != SDKPluginInitializationState.Ready)
{
    return;
}

IAttributionPlugin attribution = Nova.SDK.Get<IAttributionPlugin>();
// 页面/业务启用时订阅，停用时用 -= 对称退订。
attribution.OnAttributionResolved += HandleAttributionResolved;

attribution.TrackEvent("level_complete", new Dictionary<string, object>
{
    { "level", 10 },
});

AttributionData result = await attribution.GetAttributionAsync(cts.Token);
```

游戏服确认登录后调用 `Nova.SDK.Login(userId, userProperties)`；已有 GameLogin 路径会发出通知，不重复调用。Manager 通过 `OnSDKLogin` 交付账号，插件调用 `AppsFlyer.setCustomerUserId`，并在 `ReportCmdName` 有效时把已发布的 AppsFlyer ID 上报到业务服务器。归因事件不补发给晚订阅者，需要最近结果时用 `GetAttributionAsync(ct)`；转换失败只记录 Warning，不产生成功结果，等待必须提供取消/超时。

## 构建处理

`AppsFlyerPluginBuildProcessor` 根据同一份运行时配置处理平台工程：

- Android：确保 `Assets/Plugins/Android/gradleTemplate.properties` 中存在 `android.uniquePackageNames=false`，并注入 OneLink intent-filter。
- iOS：注入 Associated Domains、备用 URL scheme、SKAdNetwork 归因端点和 application identifier。

## 边界

- AppsFlyer 包不直接依赖 TGA 包，但初始化时需要一个已可用的 `ITrackPlugin` 发布 TGA 设备 ID 和访客 ID；缺少该能力或数据未发布时，不会继续执行 `AppsFlyer.startSDK` 和 AppsFlyer ID 发布。
- 插件不实现 `ITrackPlugin`，通用归因事件入口是 `IAttributionPlugin.TrackEvent`。
- `AppsFlyerConversionListener` 是第三方 SDK 回调适配器，不是业务查询入口。
- 初始化异常继续抛给 Manager，状态为 Failed、`IsAvailable` 不会置为 true；整体 SDK 初始化结束不是目标 SDK 成功，须检查目标 Ready。Ready 仍不证明归因回调、业务标识上报或厂商后台接收成功。
- 插件整体受 `#if !UNITY_WEBGL` 保护；Editor 的归因和 AppsFlyer ID 为 mock，仅验证本地调用链，真实归因、ATT、OneLink 和后台接收需在对应平台验证。

### 缓存、回调和释放

两个原始数据 getter 返回原字典引用，调用方修改会影响插件缓存；归因转换只复制顶层键值并字符串化，不过滤原始敏感字段。回调转发不自动切主线程，也不逐个隔离业务事件异常；解析、转换或订阅者异常可能阻止该次后续处理。原始 JSON 和归因标识可能进入现有 Debug/Warning，收集诊断时应脱敏。

`GetAttributionAsync` 已有缓存时立即返回，不检查预取消令牌；没有缓存时才等待回调并采用令牌。初始化取消只影响 TGA 数据槽等待，不能回滚此前原厂 init/start。当前 `OnDisposeAsync` 不销毁转换监听对象或重置归因缓存；结束账号会话只改变会话标记，既不取消 ID 等待，也不撤销已经派发的业务 HTTP。报告在等待 ID 后检查一次会话，网络发出后不再靠该检查回滚。

`TrackEvent` 将参数值按 `ToString()` 转成字符串，跳过空键和 null 值，不执行初始化门禁；转换或原厂异常继续传播，返回不证明后台接收。`EnableTCFDataCollection` 只改变原厂采集设置，不构造用户授权结果。

## AppsflyerDemo

示例参数表把值保存为字符串，界面预览不证明原厂最终 payload；清空表单只清本页字典，不清历史事件或插件归因缓存。登录沿用 GameLogin 对 SDK 的通知，页面不重复广播；本页没有关闭取消令牌或点击互斥，关页不会撤销登录、ID 等待或已经派发的操作。读取到空字典也不能当作真实平台已返回有效归因。

导入路径重写、旧版本目录清理、Sample 基类与启动流程的共同边界，见已安装 Framework 的 `Docs/Onboarding/PROJECT_STRUCTURE.md` 中“Sample 的导入、辅助工具与页面边界”。

## 相关

- [INDEX.md](./INDEX.md)
- [README.md](../../README.md)

Android OneLink规则树现在由内部纯`CreateAndroidManifestRules(activityName, host, pathPrefix, scheme)`集中构造，正常SDK回调和框架内部叶子预测复用同一实现；原空配置/空host与scheme早退、原读取Context.ActivityName时点及两个filter全部保持。该纯入口不执行SDK初始化/构造、properties写入或AssetDatabase Import。原回调仍在配置检查之前无条件WriteAllLines及ImportAsset，即使完整字节恰好不变也不能当无写；本次抽取没有放宽Android Action或改变导入授权，真实平台验证另行报告。
