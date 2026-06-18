# AppsFlyerPlugin

**类签名**：`public sealed partial class AppsFlyerPlugin : SDKPluginBase, IAppsFlyerPlugin, IAppsFlyerConversionData`
**命名空间**：`NovaFramework.AppsFlyerPlugin.Runtime`
**程序集**：`NovaFramework.AppsFlyerPlugin.Runtime`

AppsFlyer SDK 插件，负责初始化、事件上报、归因数据接收及深度链接处理。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `AppsFlyerPlugin.cs` | `AppsFlyerPlugin` | 主实现：所有 public / override 方法 |
| `AppsFlyerPlugin.Visitors.cs` | `AppsFlyerPlugin` | 字段、属性、常量定义 |
| `AppsFlyerPlugin.Methods.cs` | `AppsFlyerPlugin` | 私有方法（`ParseDeepLinkData`） |
| `Interfaces/IAppsFlyerPlugin.cs` | `IAppsFlyerPlugin` | 插件接口，继承 `ITrackService` |
| `Interfaces/IAppsFlyerPluginRuntimeConfig.cs` | `IAppsFlyerPluginRuntimeConfig` / `AppsFlyerPluginConfig` | 运行期配置接口及默认实现类 |

> 整体包裹于 `#if !UNITY_WEBGL`，WebGL 平台不编译。

---

## §3 继承关系

```
MonoBehaviour
  └── SDKPluginBase  (NovaFramework.Runtime, abstract)
        └── AppsFlyerPlugin  (sealed partial)
              ├── IAppsFlyerPlugin
              │     └── ITrackService
              │           └── ISDKPlugin
              └── IAppsFlyerConversionData  (AppsFlyerSDK)
```

**接口职责：**

| 接口 | 来源 | 职责 |
|------|------|------|
| `ISDKPlugin` | NovaFramework.Runtime | SDKName / Priority / OnInitializeAsync |
| `ITrackService` | NovaFramework.Runtime | TrackEvent / SetUserId / SetUserProperty |
| `IAppsFlyerPlugin` | 本插件 | SetConfig / GetAppsFlyerID / GetConversionData / GetDeepLinkData / EnableTCFDataCollection |
| `IAppsFlyerConversionData` | AppsFlyerSDK | 四个归因/深链回调 |

---

## §4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Name` | `string`（override） | `"AppsFlyer"` | SDK 唯一标识名称，由 SDKManager 用于插件索引 |
| `Priority` | `int`（override） | `90` | 初始化优先级，值越小越先初始化 |
| `m_ConversionData` | `Dictionary<string, object>` | `null` | 归因数据缓存，由 `onConversionDataSuccess` 回调填充 |
| `m_DeepLinkData` | `Dictionary<string, object>` | `null` | 深度链接数据缓存，由 `ParseDeepLinkData` 填充 |
| `m_Attribution` | `AttributionData` | `null` | 解析后的归因数据缓存；`BuildAndPublishAttribution` 填充，非 null 时 `GetAttributionAsync` 立即返回 |
| `m_OnAttributionResolved` | `Action<AttributionData>` | `null` | `IAttributionPlugin.OnAttributionResolved` 事件内部委托链 |
| `m_ConversionListener` | `AppsFlyerConversionListener` | `null` | 挂载在独立 GameObject 上的归因回调监听器 |
| `m_EventManager` | `IEventManager` | `null` | 事件管理器引用；`OnInitializeAsync` 末尾取得，`OnDisposeAsync` 开头清空 |

---

## §5 完整公开 API

### IAppsFlyerPlugin — AF 专有能力

```csharp
// 初始化前注入运行期配置；已初始化后调用无效（输出 Warning 后直接返回）
void SetConfig(IAppsFlyerPluginRuntimeConfig config)

// 获取 AppsFlyer 分配的设备唯一 ID
string GetAppsFlyerID()

// 获取缓存的归因数据；onConversionDataSuccess 前返回 null
Dictionary<string, object> GetConversionData()

// 获取缓存的深度链接数据；未触发深度链接时返回 null
Dictionary<string, object> GetDeepLinkData()

// 控制 TCF（Transparency and Consent Framework）数据采集开关
void EnableTCFDataCollection(bool shouldCollect)
```

### ITrackService — 埋点服务

```csharp
// 上报无参事件；内部以空 Dictionary<string,string> 调用 AppsFlyer.sendEvent
void TrackEvent(string eventName)

// 上报有参事件；自动将 object value 转为 string，跳过空 key 或 null value
void TrackEvent(string eventName, Dictionary<string, object> parameters)

// 设置用户 ID；调用 AppsFlyer.setCustomerUserId(userId)
// 由 SDKEventData.UserLogin 事件触发（OnInitializeAsync 末尾 Subscribe，OnDisposeAsync 开头 Unsubscribe），
// 也可由业务层直接调用。
void SetUserId(string userId)

// 空实现；AF SDK 无 UserProperty 概念，仅为满足 ITrackService 契约
void SetUserProperty(string key, string value)
```

### SDKPluginBase — 生命周期

```csharp
// 异步初始化 AF SDK；优先使用注入配置，否则读取配置表
override UniTask OnInitializeAsync()
```

### IAppsFlyerConversionData — 归因/深链回调

```csharp
// 归因数据获取成功；更新 m_ConversionData；首次启动时同步解析深度链接
void onConversionDataSuccess(string conversionData)

// 归因数据获取失败；输出 Warning 日志
void onConversionDataFail(string error)

// 深度链接打开（热启动/冷启动）；更新 m_DeepLinkData
void onAppOpenAttribution(string attributionData)

// 深度链接打开失败；输出 Warning 日志
void onAppOpenAttributionFailure(string error)
```

### IAppsFlyerPluginRuntimeConfig — 运行期配置接口

```csharp
string DevKey { get; }    // AppsFlyer Dev Key，对应 AFDevKey
string AppId { get; }     // 商店/平台应用 ID，对应 TbCommonConfigs.MGAppID
bool LogEnable { get; }   // 是否开启 AF 调试日志，对应 AFLogEnable
```

### AppsFlyerPluginConfig — 配置默认实现

```csharp
// 构造器
AppsFlyerPluginConfig(string devKey, string appId, bool logEnable = false)
```

---

## §6 初始化流程状态

```
[未初始化]
    │
    │  SetConfig(config)       ← 可选；m_InitOver == false 时有效
    │                            m_InitOver == true 时输出 Warning 并跳过
    ▼
[配置就绪]
    │
    │  SDKManager 调用 OnInitializeAsync()
    │
    ├─ DevKey 或 AppId 为空 ──► [初始化跳过]（输出 Warning，m_InitOver 保持 false）
    ├─ SDKComponent 未就绪   ──► [初始化跳过]（同上）
    │
    │  AppsFlyer.initSDK(devKey, appId, this)
    │  EnableTCFDataCollection(true)
    │  AppsFlyer.setAdditionalData(customData)     ← 含 TGA DistinctId / DeviceId
    │  [iOS] waitForATTUserAuthorizationWithTimeoutInterval(60)
    │  AppsFlyer.startSDK()
    │  [Editor] 注入模拟归因数据 → onConversionDataSuccess(editorConversionData)
    │  m_InitOver = true
    ▼
[初始化完成]
    │
    │  AF SDK 异步回调（任意时刻）
    ├─ onConversionDataSuccess  ──► m_ConversionData 更新
    │                               首次启动时 ParseDeepLinkData → m_DeepLinkData 更新
    ├─ onConversionDataFail     ──► Warning 日志
    ├─ onAppOpenAttribution     ──► m_DeepLinkData 更新
    └─ onAppOpenAttributionFailure ──► Warning 日志
```

**状态不可逆：** `m_InitOver` 一旦置 `true` 不再重置，不支持重新初始化。

---

## §7 事件订阅生命周期

`AppsFlyerPlugin` 在 `OnInitializeAsync` 末尾（SDK 初始化完成后）订阅 `SDKEventData.UserLogin`，在 `OnDisposeAsync` 开头退订。

```
OnInitializeAsync（末尾，#if !UNITY_WEBGL 块内）：
  m_EventManager = FrameworkManagersGroup.GetManager<IEventManager>()
  m_EventManager.Subscribe<SDKEventData.UserLogin>(OnUserLogin)

OnDisposeAsync（开头）：
  m_EventManager.Unsubscribe<SDKEventData.UserLogin>(OnUserLogin)
  m_EventManager = null

private void OnUserLogin(object sender, EventData e):
  if (e is SDKEventData.UserLogin login)
      SetUserId(login.UserId)   // → AppsFlyer.setCustomerUserId(userId)
```

整体代码包裹于 `#if !UNITY_WEBGL`，WebGL 平台不编译。

**直接方法组订阅：** Subscribe/Unsubscribe 直接传 `OnUserLogin` 方法组，CLR 委托 Equals 按 Target+Method 比对，同一实例方法的两次方法组转换结果 Equals，Unsubscribe 可正确配对，无需字段缓存。

---

## §8 初始化时序

```
SDKComponent.Start()
    │
    ├─ (可选) sdkComponent.GetPlugin<IAppsFlyerPlugin>().SetConfig(config)
    │         ← 必须在 SDKComponent 启动初始化流程之前调用
    │
    ├─ ITGAPlugin.OnInitializeAsync()      ← Priority 低于 10，必须先完成
    │         TGA 初始化后 GetDistinctId() / GetDeviceId() 才可用
    │
    └─ AppsFlyerPlugin.OnInitializeAsync()
              ├─ sdkComponent.GetPlugin<ITGAPlugin>().GetDistinctId()
              └─ sdkComponent.GetPlugin<ITGAPlugin>().GetDeviceId()
```

**依赖约束：**

- `AppsFlyerPlugin.OnInitializeAsync` 内部同步调用 `ITGAPlugin.GetDistinctId()` 和 `GetDeviceId()`。
- TGAPlugin 的 Priority 必须小于 10（数值更小 = 更早初始化），否则 AF 初始化时 TGA 尚未就绪，附加数据将为空或抛出异常。
- `SetConfig` 必须在 SDKManager 触发 `OnInitializeAsync` **之前**调用，否则注入无效。

---

## §10 常见误区

**误区 1：初始化后再调用 SetConfig**

```csharp
// 错误：SDKComponent 已完成初始化后调用，SetConfig 直接返回
sdkComponent.GetPlugin<IAppsFlyerPlugin>().SetConfig(config);
```

`SetConfig` 通过 `m_InitOver` 守卫，初始化完成后调用会输出 Warning 并忽略。必须在 SDKManager 触发插件初始化之前注入。

---

**误区 2：不注入 SetConfig 期望从配置表自动读取**

当前实现中 `m_RuntimeConfig` 为 `null` 时，DevKey 和 AppId 均以 `string.Empty` 参与判断，初始化会因空值检查直接跳过并输出 Warning。若不使用外部注入，需在 `OnInitializeAsync` 中补充从配置表读取的逻辑（当前代码尚未实现此路径）。

---

**误区 3：初始化前读取 GetConversionData / GetDeepLinkData**

`m_ConversionData` 和 `m_DeepLinkData` 初始值为 `null`，在 AF SDK 回调前读取均返回 `null`。调用方需做空值保护：

```csharp
var data = afPlugin.GetConversionData();
if (data == null) return;
```

---

**误区 4：期望 SetUserProperty 产生实际效果**

AF SDK 无用户属性概念，`SetUserProperty` 是空实现，调用不会向 AF 上报任何数据。

---

**误区 5：TrackEvent 的参数类型**

`TrackEvent(string, Dictionary<string, object>)` 接受 `object` 值，内部自动转 `string`。空 key 或 `null` value 的条目会被静默跳过，不会抛异常，但也不会上报。

---

## §11 使用示例

### 注入配置并触发初始化

```csharp
// 在 SDKComponent 启动初始化之前，从业务配置表构造并注入
var afPlugin = sdkComponent.GetPlugin<IAppsFlyerPlugin>();
var config = new AppsFlyerPluginConfig(
    devKey: tbAppsFlyerConfigs.AFDevKey,
    appId: tbCommonConfigs.MGAppID,
    logEnable: tbAppsFlyerConfigs.AFLogEnable
);
afPlugin.SetConfig(config);
// 之后 SDKManager 自动调用 OnInitializeAsync，无需手动触发
```

### 事件上报

```csharp
var afPlugin = sdkComponent.GetPlugin<IAppsFlyerPlugin>();

// 无参事件
afPlugin.TrackEvent("level_complete");

// 有参事件
afPlugin.TrackEvent("purchase", new Dictionary<string, object>
{
    { "af_revenue", 9.99 },
    { "af_currency", "USD" },
    { "item_id", "gem_pack_100" }
});
```

### 读取归因数据

```csharp
var afPlugin = sdkComponent.GetPlugin<IAppsFlyerPlugin>();

var conversionData = afPlugin.GetConversionData();
if (conversionData != null && conversionData.TryGetValue("media_source", out var source))
{
    Debug.Log($"归因渠道: {source}");
}

var deepLinkData = afPlugin.GetDeepLinkData();
if (deepLinkData != null && deepLinkData.TryGetValue("deep_link_value", out var linkValue))
{
    Debug.Log($"深度链接值: {linkValue}");
}
```

### 设置用户 ID

```csharp
afPlugin.SetUserId(currentUserId);
```

---

## §13 关联文档

| 文档 | 说明 |
|------|------|
| [INDEX.md](./INDEX.md) | 本包文档总索引 |
