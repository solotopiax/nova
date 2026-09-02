# TGAPlugin

## 1. 简介

`TGAPlugin` 是 Nova 对 ThinkingAnalytics 的运行时封装，当前实现：

- 继承 `SDKPluginBase`
- 实现 `ITrackPlugin`
- 实现 `IDeviceIdProvider`

它不再暴露旧文档里的 `ITGAPlugin` 专属接口栈，而是直接对齐主框架当前的通用 SDK 契约。

## 2. 公开 API

### 2.1 埋点

| 成员 | 说明 |
|---|---|
| `TrackEvent(string)` | 上报无参事件 |
| `TrackEvent(string, Dictionary<string, object>)` | 上报字典参数事件 |
| `TrackEvent(TrackEvent)` | 上报统一载荷事件 |
| `TrackEvent(string, string)` | 上报 JSON 字符串属性事件 |
| `TrackEvent(string, Dictionary<string, object>, DateTime, TimeZoneInfo)` | 上报带业务时间的事件 |
| `TrackFirst(...)` | 首次事件 |
| `TrackUpdatable(...)` | 可更新事件 |
| `TrackOverwritable(...)` | 可覆写事件 |

### 2.2 身份与设备

| 成员 | 说明 |
|---|---|
| `SetUserId(string)` / `Login(string)` | 绑定账号 ID |
| `Logout()` | 清除账号绑定 |
| `SetDistinctId(string)` / `GetDistinctId()` | 手动设置或读取访客 ID |
| `GetDeviceId()` | 读取 TGA 设备 ID |
| `IDeviceIdProvider.GetDeviceID()` | 对外提供统一设备 ID 契约 |

`TGAPluginConfig.AssignDeviceIdToDistinctId` 开启时，插件初始化后会先将 TGA `DeviceId` 写入 `DistinctId`，再发布 `TGADistinctId` 数据槽位。

### 2.3 用户属性

| 成员 | 说明 |
|---|---|
| `UserSet(...)` | 覆盖式设置属性 |
| `UserSetOnce(...)` | 首次写入属性 |
| `UserAdd(...)` | 数值属性累加 |
| `UserAppend(...)` | 列表属性追加 |
| `UserUnset(...)` | 删除指定属性 |
| `UserDelete()` | 删除当前用户全部属性 |

### 2.4 公共属性与采集控制

| 成员 | 说明 |
|---|---|
| `SetSuperProperty(...)` / `SetSuperProperties(...)` | 静态公共属性 |
| `UnsetSuperProperty(...)` / `ClearSuperProperties()` | 清理静态公共属性 |
| `SetDynamicSuperProperty(...)` / `SetDynamicSuperProperties(...)` | 动态公共属性 |
| `RemoveDynamicSuperProperty(...)` / `ClearDynamicSuperProperties()` | 清理动态公共属性 |
| `GetDynamicSuperProperties()` | 供 TGA SDK 回调读取动态属性快照 |
| `TimeEvent(string)` | 事件计时 |
| `Flush()` | 立即冲刷本地缓存 |
| `EnableTracking(bool)` | 开关采集 |
| `SetTrackStatus(int)` | 设置 SDK TrackStatus |
| `CalibrateTime(long)` / `CalibrateTimeWithNtp(string)` | 时间校准 |

## 3. 初始化与配置

- `Priority => 10`，作为当前 SDK 初始化链的首个分桶，先于依赖 TGA 标识数据的插件初始化

- `OnInitializeAsync(...)` 会读取 `TGAPluginConfig`
- `Mode` 与 `TimeZone` 使用包内枚举保存，初始化时转换为 `TDMode` / `TDTimeZone` 后写入 `TDConfig`
- `AppID` 为空或 `ServerCmdName` 无法解析为上报地址时，插件会跳过初始化
- 初始化成功后会：
  - 按配置将 `DeviceId` 同步到 `DistinctId`
  - 注册框架公共属性
  - 创建 `TGADynamicSuperPropertyListener`
  - 监听 `SDKEventData.UserLogin`
  - 通过 `TGAReportNetService` 上报 TGA 标识
  - 异步监听 `IAuthPlugin` 发布的 `SDKDataKeys.OpenId` / `SDKDataKeys.ThirdLoginProvider`，并通过 `UserSet` 写入 `nova_openid` / `nova_third_login_provider`

### 3.1 初始化顺序

1. 缓存 `TGAPluginConfig` 与 `TGAReportNetService`。
2. 校验 `AppID` 与 `ServerCmdName`。
3. 通过 Nova Network 模块将 `ServerCmdName` 解析为真实上报 URL。
4. 构造 `TDConfig`，写入 `Mode`、`TimeZone`，并调用 `TDAnalytics.Init(...)`。
5. 按配置同步 `DeviceId` 到 `DistinctId`，再写入框架默认属性。
6. 创建动态公共属性监听器并启用自动采集。
7. 发布 `TGADevicesId` / `TGADistinctId` 数据槽位，并启动 AppsFlyer ID 与第三方登录标识的异步桥接。
8. 订阅登录事件，登录后发布 `TGAAccountId` 并向业务服务器上报 TGA 标识。

## 4. 使用示例

```csharp
TGAPlugin tga = /* 已从 SDKComponent / SDKManager 取得插件实例 */;

tga.TrackEvent("ui_click", new Dictionary<string, object>
{
    ["view"] = "shop",
    ["button"] = "buy",
});

tga.UserSet("country", "CN");
tga.SetDynamicSuperProperty("session_id", "abc-123");
```

## 5. 关联

- 配置类型：[TGAPluginConfig.md](./TGAPluginConfig.md)
