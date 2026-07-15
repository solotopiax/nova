# DataMasterPlugin

DataMaster SDK 的 Nova 接入插件。继承 `SDKPluginBase`，由 `SDKManager` 统一实例化、注入配置、编排生命周期。ABTest 能力经本类型的公开方法暴露，业务通过 `SDKManager.Get<DataMasterPlugin>()` 获取实例后调用。

## 接入前置

1. **在 ConfigWindow 启用并配置**：DataMasterPlugin 不属于任何 SDK 能力族（埋点 / 广告 / 归因 / 账号 / 云服务 / 支付），因此**不通过 SDK 面板的族选型启用**，而是直接在 ConfigWindow 勾选启用 `DataMasterPluginConfig`（写入 `ConfigMaster.EnabledSDKs`），并填写：
  - `AppId`：数据平台后台分配的应用 ID。
  - `AesKey`：数据平台后台提供的密钥（用于请求 / 响应 / 本地缓存加解密）。
  - `DefaultConfig`：随包发布的默认配置文本（策划导出的开发客户端配置），作为断网 / 服务端未下发时的兜底。
  > `ConfigMaster.EnabledSDKs` 是 SDK 启用的**唯一真相源**。`SDKManager.InitializeAsync` 会据此对「已启用但不属能力族、未在 SDK 面板选型」的插件补充实例化（见框架 `InstantiateFromEnabledConfigs`），因此启用 DataMaster **无需**、也无法在 SDK 面板操作。
2. **登录闭环**：业务登录成功后调用 `ISDKManager.Login(userId)`（`Nova.SDK.Login(userId)`）。插件订阅了 `SDKEventData.UserLogin`，收到后自动携带用户属性与设备 ID 向服务端拉取实验配置。这一步是 DataMaster 拉取的触发点——不登录则只有本地默认配置、不发起服务端拉取。



## 生命周期


| 阶段  | 行为                                                                                  |
| --- | ----------------------------------------------------------------------------------- |
| 初始化 | 校验配置 → 解析默认配置文本 → 初始化厂商 DataMaster 本地库（SQLite）→ 订阅登录与归因事件。配置缺失时记 Warning 并降级（读参返回兜底值）。 |
| 登录  | 收到 `UserLogin` → 携带 userId / deviceId / 用户属性向服务端拉取 → 落库 → 触发 `OnConfigRefreshed`。   |
| 释放  | 退订登录与归因事件，取消归因等待。厂商单例常驻，无显式 shutdown。                                              |




## 公开 API


| 成员                                                                                 | 说明                                                                               |
| ------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------- |
| `T GetParamValue<T>(string topicId, string paramName, T fallback = default)`         | 读取实验参数并反序列化；优先服务端下发值，无则本地默认值，失败返回 fallback。`topicId` 口径见下。                       |
| `string GetParamValueJson(string topicId, string paramName)`                         | 读取参数原始 JSON 字符串，无值返回 null。                                                       |
| `void MarkExposure(string topicId)`                                                  | 以当前时间标记玩家首次曝光于该主题实验。**应在玩家真正接触到实验功能处调用，通常一次实验只调一次。**                             |
| `void SetExposureTimeMs(string topicId, long exposureTimeMs)`                        | 以显式时间戳标记曝光（高级用法）。                                                                |
| `void LogExperimentEvent(string eventName, double value)`                            | 上报实验指标事件；框架在调用时实时构造标准用户上下文。                                                       |
| `void LogExperimentEvent(string eventName, double value, Dictionary<string, object> extraContext)` | 上报实验指标事件，并把字典原样作为本次事件的 `ExtraContext`；不缓存、不合并。                                  |
| `void SetUserProperty(string key, object value)`                                     | 设置分流用户属性（如等级 / 国家 / 是否 VIP）；须在登录触发拉取前设置方能参与本次分流。                                 |
| `void ApplyRequiredUserProperties()`                                                 | 立即刷新 `app_version` / `install_time` 的兼容接口；正常流程会在每次拉取前自动刷新，无需业务调用。                 |
| `int GetAppVersionCode()`                                                            | 取整数版本号（`app_version` 口径），`Application.version` 三段合成 `major*1e6+minor*1e3+patch`。 |
| `long GetInstallTimeMs()`                                                            | 取首次启动毫秒时间戳（`install_time` 近似值，13 位），PlayerPrefs 持久化保证单设备稳定。                      |
| `IReadOnlyList<string> GetTopicNames()`                                              | 枚举业务可读的主题名（topicId 口径）。从默认配置 `Params.Keys` 缓存；业务不预知主题名时运行时取。                     |
| `string GetDeviceId()`                                                               | 获取当前设备 ID（与拉取 / 上报口径一致）。                                                         |
| `void ClearRuntimeCache()`                                                           | 清服务端下发值 / 实验状态 / 事件序号，回到仅默认配置初始态。配合换 uid 模拟新设备首次分桶。                              |
| `event Action OnConfigRefreshed`                                                     | 服务端配置拉取成功后触发；业务据此清缓存重读参数。                                                        |
| `event Action<string> OnConfigRefreshFailed`                                         | 拉取失败触发，参数为错误信息。                                                                  |
| `event Action<string> OnRefreshTriggered`                                            | 拉取发起时触发，参数为本次传参摘要（userId / deviceId / userProperties）。                           |




### topicId 口径（重要）

`GetParamValue` / `GetParamValueJson` / `MarkExposure` 的 `topicId` 入参，传的是**服务端** `response.Params` **字典的 key**（如 `cfa3ee9f`，落库为 `dm_param_table.topic_name`），**不是** `experiment.topicId`（如 `topic_VgZav6`，后者仅用于上报上下文协商，不参与检索）。

该 key 由服务端生成、业务无法预知，因此**不要硬编码**，应运行时经 `GetTopicNames()` 获取（其从随包默认配置 `Params.Keys` 缓存而来）。

### 必传分流属性

`SetUserProperty` 中，`app_version` 与 `install_time` 为**必传字段**（缺失影响服务端分流命中）：


| 属性             | 类型           | 口径                                                                   |
| -------------- | ------------ | -------------------------------------------------------------------- |
| `app_version`  | number（int）  | 整数版本号，`major*1_000_000 + minor*1_000 + patch`（`"1.0.0"` → `1000000`） |
| `install_time` | number（long） | 首次安装毫秒时间戳（13 位 = ms）                                                 |


> 两条必传属性的合成 / 记录已内置在插件里，并会在**每次服务端拉取请求发出前强制更新**。业务无需调用 `ApplyRequiredUserProperties()`，只需在登录拉取前通过 `SetUserProperty` 设置 `country_code` 和其他项目分流属性。

### 事件用户上下文

每次 `LogExperimentEvent` 都会新建 `DMUserContext`，不会复用上一次事件数据。框架只在数据可用时填充字段：

| 字段 | 实时来源 |
| --- | --- |
| `PlayerId` | 最近一次登录 UID |
| `DeviceId` | `IDeviceIdProvider`，无可用值时回退 Unity 设备 ID |
| `MediaSource` / `CampaignName` | 当前 `IAttributionPlugin` 归因结果；尚未就绪时留空 |
| `InstallChannel` | `Nova.Config.Channel` |
| `InstallTimeMs` | 框架记录的首次启动毫秒时间戳 |
| `CountryCode` | 当前 `m_UserProperties["country_code"]` |
| `LanguageCode` | `Nova.Localization.Language` 对应的 BCP 47 标识 |
| `AppVersion` | 框架整数版本号 |
| `OsVersion` | Android 填 `Android`，iOS 填 `iOS`，其他平台留空 |
| `ExtraContext` | 三参数重载当次传入的字典 |

归因数据通过 `IAttributionPlugin` 异步缓存，不阻塞 DataMaster 初始化。某次事件发生时归因尚未返回，则该次不填归因字段；后续事件会使用最新结果。



## 使用示例

```csharp
var dm = sdkManager.Get<DataMasterPlugin>();

// 分流属性（登录前设置）；app_version / install_time 会在拉取前自动更新
dm.SetUserProperty("country_code", "US");

// topicId 传 Params 字典 key，运行时取（不硬编码）
string topicId = dm.GetTopicNames().FirstOrDefault();

// 读取实验参数（带兜底）
int reviveCost = dm.GetParamValue(topicId, "revive_cost", fallback: 100);

// 复杂配置类型
var rule = dm.GetParamValue<RewardRule>(topicId, "reward_rule", fallback: null);

// 玩家真正打开复活面板时打曝光
dm.MarkExposure(topicId);

// 上报事件；标准上下文由框架实时填充，字典仅随本次事件上传
dm.LogExperimentEvent(
    "revive_success",
    1,
    new Dictionary<string, object> { ["source"] = "gameplay" });

// 配置刷新后重读
dm.OnConfigRefreshed += () => ReloadConfigs();
```



## Demo 按钮 ↔ API 对照

`DataMasterDemo` 交互区把常用接口拆成独立按钮，客户端接入只需照搬这些调用。按 ABTest 流程顺序（清缓存为辅助）：


| 按钮             | 调用的 API                                                         | 作用                                                                    |
| -------------- | --------------------------------------------------------------- | --------------------------------------------------------------------- |
| 清理 SDK 缓存      | `ClearRuntimeCache()`                                           | 清服务端下发值 / 实验状态 / 事件序号，配合换 uid 模拟新设备                                   |
| 设置分流属性         | `SetUserProperty(key, value)`                                  | 按需设置 country_code 等项目属性；app_version / install_time 在拉取前自动更新          |
| 模拟登录并拉取        | `Nova.Network.Kit<Login>().Async(…, forceNewAccount:true)`      | 登录成功后 kit 自动 `Nova.SDK.Login` → 插件订阅 `UserLogin` → 拉取                 |
| 读取实验参数         | `GetParamValue<T>(topicId, paramName, fallback)`                | 读分组值并反序列化                                                             |
| 读取实验参数（通过JSON） | `GetParamValueJson(topicId, paramName)`                         | 读分组值原始 JSON 字符串                                                       |
| 标记曝光           | `MarkExposure(topicId)`                                         | 玩家接触实验功能时计入分母                                                         |
| 上报实验事件         | `LogExperimentEvent(eventName, value, extraContext)`            | 计入实验分子，并演示只随当次事件上传的 ExtraContext                                  |


> `topicId` 由 demo 经 `GetTopicNames()` 运行时解析，不硬编码（见「topicId 口径」）。



## 构建环境（正式 / 测试域名切换）

底层厂商 DataMaster SDK 用编译宏 `PRODUCTION_PACKAGE` 区分请求环境（详见 `官方SDK技术文档.md` §1、§11）：


| `PRODUCTION_PACKAGE` | 配置拉取域名                     | 事件上报域名                   |
| -------------------- | -------------------------- | ------------------------ |
| 未定义（默认）              | `features-dev.starlus.net` | `report-dev.starlus.net` |
| 已定义                  | `features.starlus.net`     | `report.starlus.net`     |


**业务无需手动管理该宏**：本包 Editor 层的 `DataMasterPluginBuildProcessor`（继承框架 `NovaSDKBuildProcessor`）在**构建时**按开发模式自动处理：

- 开发模式 = **Release** → 构建产物注入 `PRODUCTION_PACKAGE`（走正式域名）；
- 开发模式 = **Debug** → 构建产物不含该宏（走测试域名）。

该处理是**临时的**：编译前记录工程原本的宏状态并存入 `SessionState`，构建完成后精确复原（原有则保留、原无则移除），**不污染工程持久 PlayerSettings**；且仅当本插件已在 `ConfigMaster` 启用时才注入，未启用的工程不受影响。

> 开发模式在 `ConfigWindow` 切换（`ConfigMaster.CurrentDevelopMode`），导出后写入 `ConfigRuntimeSO.DevelopMode`，BuildProcessor 据此判定。改开发模式后需重新构建才切换环境域名。



## 注意事项

- **Editor 只读默认值**：厂商实现在 `UNITY_EDITOR` 下只返回本地默认值，忽略服务端下发值。服务端下发 / 实验命中需在真机验证。
- **后台配置须已发布才生效**：数据平台后台的主题 / 测试集 / 目标受众 / 分组配置等所有步骤，**必须在后台点击「发布」后**才会对客户端拉取生效。改了没发布，客户端拉取到的仍是旧值。
- **曝光打准是关键**：曝光决定实验数据的分母，必须打在玩家真正接触实验功能的位置，不能一登录就打，也不能等行为完成后才打。原理见 `ABTest扫盲.md`。
- **事件上报无重试队列**：厂商当前实现对上报失败不重试，有可靠性要求需业务侧自行补偿。



## 客户端认知范围

客户端**只需关注上表这几个公开 API 的调用**（读参 / 读原始 JSON / 曝光 / 上报事件 / 设置分流属性 / 触发拉取），其余无需理会：

- 后台的主题（topic）下有若干测试集（如 `auto_enter_game`）、各自的目标受众与分组配置（cases）——这些是**服务端的分流职责**，客户端不关心怎么分组、cases 表长什么样。
- 客户端拿到的 `values` 就是当前身份命中分组后的最终值，`caseId` 仅透传回服务端做上下文协商，客户端不解读。
- 客户端要做的只有三件：**读参**（让分组值作用于业务）→ **曝光**（计入实验分母）→ **上报事件**（计入分子）。三者构成完整闭环，缺读参则实验对玩家不生效。
