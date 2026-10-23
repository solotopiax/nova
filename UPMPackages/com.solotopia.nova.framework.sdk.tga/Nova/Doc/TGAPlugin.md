# TGAPlugin

## 1. 简介

`TGAPlugin` 是 Nova 对 ThinkingAnalytics 的运行时封装，当前实现：

- 继承 `SDKPluginBase`
- 实现 `ITrackPlugin` 与可选 `IUserPropertyPlugin`
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

WebGL 下由 ThinkingAnalytics PC/WebGL 实现生成随机 GUID，并通过 PlayerPrefs 持久化作为 `DeviceId`。该值不是硬件标识，清除浏览器站点数据、使用无痕模式或更换站点来源后会重新生成。

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
| `CalibrateTime(long)` | 使用业务服务器提供的时间戳校准 |
| `CalibrateTimeWithNtp(string)` | 通过 NTP 服务器校准；WebGL 下会提示并跳过，请改用 `CalibrateTime(long)` |

## 3. 初始化与配置

- `Priority => 10` 只决定稳定通知和逆序释放顺序；初始化按已声明的能力依赖调度。需要 TGA 标识的插件通过 `ITrackPlugin` 依赖与数据槽等待接入，不能从 Priority 数字推导初始化先后。

- `OnInitializeAsync(...)` 会读取 `TGAPluginConfig`
- `Mode` 与 `TimeZone` 使用包内枚举保存，初始化时转换为 `TDMode` / `TDTimeZone` 后写入 `TDConfig`
- `AppID`、`ServerCmdName` 为空或上报地址解析失败时抛出初始化异常，由 SDKManager 隔离该插件失败；整体初始化结束不代表 TGA Ready。
- 初始化成功后会：
  - 按配置将 `DeviceId` 同步到 `DistinctId`
  - 注册框架公共属性
  - 创建 `TGADynamicSuperPropertyListener`
  - 通过 `ISDKLoginReceiver` 接收 Manager 交付的当前账号
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
8. Manager 在目标插件 Ready 后通过 `OnSDKLogin` 交付当前账号，随后发布 `TGAAccountId` 并向业务服务器上报 TGA 标识；插件不自行订阅旧登录事件。

初始化会调用原厂 SDK、设置全局属性并创建动态属性监听对象；中途异常没有整体回滚。首次取得 `nova_first_version` 时若存档尚无该项，会写入并保存 FileFragment 分类，因此初始化中的默认属性构造不是纯查询。普通公共属性、框架公共属性、动态属性分别维护，调用原厂静态公共属性入口不会自动改写框架自己的属性字典。

### 3.2 属性与生命周期边界

动态属性的业务字典在锁内浅复制后与框架字典合并，同名业务值优先，值对象仍共享；批量 Set 是逐项覆盖，失败不回滚前面项目。清空或移除业务动态属性后，框架同名属性仍可能出现。静态公共属性、UserSet、UserSetOnce 与动态属性各自独立；`UserSet`/`UserSetOnce` 可在业务字典为空时上传框架属性，不能把空参数理解成无操作。UserAdd 只接受 int/long/float/double，仍不排除 NaN/Infinity；UserUnset 列表逐项发送，首个异常后停止。多数厂商透传入口没有 Ready 门禁或上传确认，JSON 字符串重载也不在本地解析。

首次版本保存会忽略 Save 的布尔返回值，已有空值不修复，因此读到版本不证明已耐久落盘。当前 `OnDisposeAsync` 没有清属性、销毁/解绑动态监听器或关闭原厂 SDK；监听对象依赖 Unity 父节点生命周期，外部保留的监听器仍可能回调旧插件。第三方登录桥接分别等待 OpenId 与 provider，没有默认超时，多个账号可覆盖同名框架属性。

结束业务会话只更新 session 标记，不执行原厂 Logout、取消标识等待或撤回已发 HTTP；上报仅在派发前核对一次 session。标识、OpenID 和原始属性可能进入现有日志，诊断资料应脱敏。`CalibrateTime` 单位为 Unix 毫秒；WebGL NTP 入口只告警跳过。`EnableTracking(false)` 映射 Pause，不等于删除缓存或停止原厂 SDK。

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

## 可选一次性属性能力

`TGAPlugin` 实现框架 `IUserPropertyPlugin`，复用已有 `UserSetOnce(Dictionary<string, object>)`。消费者按能力查询无需依赖 TGA 类型；既有用户属性合并和仅首次写入语义保持不变。微信引力通过该能力自动提交非敏感归因属性，等待 TGA 就绪不会阻塞微信登录或 SDK 初始化。启用该功能需安装含 `IUserPropertyPlugin` 的 Framework 版本；正式发布时须提升依赖下界。
