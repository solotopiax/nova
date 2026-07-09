# DataMaster SDK 技术文档

`DataMaster` 是客户端配置与实验参数模块，提供本地默认配置初始化、服务端配置拉取、参数读取、实验曝光时间记录和事件上报能力。模块运行时会把参数写入本地 SQLite 数据库，并对本地缓存值做 AES 加密存储。

## 1. 依赖与程序集

- 命名空间：`StarlusSDK.DataMaster`
- 程序集：`DataMaster`
- Unity 依赖：`UnityEngine.Networking`
- 第三方依赖：`Newtonsoft.Json`、内置 `sqlite-net`、各平台 SQLite native plugin
- 当前生产/测试环境由编译宏 `PRODUCTION_PACKAGE` 用于区分 正式/测试 ：
  - 未定义 `PRODUCTION_PACKAGE`：请求 `features-dev.starlus.net`、上报 `report-dev.starlus.net`
  - 定义 `PRODUCTION_PACKAGE`：请求 `features.starlus.net`、上报 `report.starlus.net`

## 2. 核心数据模型

默认配置和服务端配置使用 `DMGetParamsResponse`：

```csharp
public class DMGetParamsResponse
{
    public int Environment;
    public Dictionary<string, DMParams> Params;
}

public class DMParams
{
    public DMExperiment Experiment;
    public Dictionary<string, int> Schema;
    public Dictionary<string, object> Values;
}
```

`Params` 的 key 是 `topicId`。每个 topic 下：

- `Schema` 定义参数名和参数类型。
- `Values` 保存参数默认值或服务端下发值。
- `Experiment` 保存实验上下文，包括 `topicId`、`caseId`、`enterTimeMs`、`exposureTimeMs`。

## 3. 初始化

初始化入口：

```csharp
DataMaster.Instance.Initialize(appId, defaultConfig, aesKey);
```

参数说明：

- `appId`：应用 ID，会写入拉取和上报请求头/请求体。(服务端提供)
- `defaultConfig`：本地默认配置。(加载策划提供的本地配置)
- `aesKey`：服务端密钥，用于请求体、响应体和本地缓存值加解密。(服务端提供)

初始化时会执行：

1. 创建或打开本地数据库：`Application.persistentDataPath/dm_param_table.db`
2. 创建参数表 `dm_param_table`
3. 创建实验表 `dm_experiment_table`
4. 加载事件上报序号 `DM_SEQ_CACHE`
5. 将本地默认配置写入数据库的 `default_value`

注意：

- `Initialize` 必须先于 `RefreshFromServer`、`GetParamValue`、`LogEvent` 调用。
- 如果未初始化，`RefreshFromServer` 和 `LogEvent` 会直接返回。
- 如果本地库被篡改或损坏，SDK 会 drop 表并用默认配置重建。

## 4. 解析默认配置

入口：

```csharp
DMGetParamsResponse config = DataMaster.Instance.ParseConfigJson(json);
```

行为：

- 空字符串返回空的 `DMGetParamsResponse`。
- 解析前会移除 JSON 顶层字段 `descriptions`。
- JSON 非法时会打印错误并抛出异常。
- 默认配置文件应随包体发布，作为断网或服务端未下发时的兜底配置。
- 默认配置中的 `schema` 是本地参数名来源；初始化会按 `schema` 写入默认参数。

## 5. 拉取服务端配置

入口：

```csharp
DataMaster.Instance.RefreshFromServer(
    userId,
    deviceId,
    userProperties,
    onSuccess,
    onError);
```

参数说明：

- `userId`：玩家 ID，不能为空；为空时 SDK 打印错误并返回。
- `deviceId`：设备 ID。
- `userProperties`：用于服务端分流和规则匹配的用户属性。
- `onSuccess`：成功处理服务端响应并落库后回调。
- `onError`：网络失败、解密失败、解析失败或落库失败时回调错误信息。

请求流程：

1. 读取本地 `dm_experiment_table`，构造已有实验上下文。
2. 合并 `userProperties`。
3. JSON 序列化 `DMGetParamsRequest`。
4. 尝试 GZip 压缩。
5. AES 加密请求体。
6. POST 到 `RequestUrl`。
7. 解密响应体，按响应头 `x-content-encoding: gzip` 决定是否解压。
8. `ParseConfigJson` 解析响应。
9. `ProcessServerConfig` 将服务端值写入 `new_value`，并更新实验状态。


使用流程：

- 启动时先使用默认配置完成业务初始化，再后台拉取服务端配置。
- 拉取成功后广播业务事件，让各业务 loader 清缓存并重新读取参数。
- 拉取失败建议做指数退避重试，避免启动时网络波动导致配置长期不更新，看策划需求。

## 6. 获取配置

获取原始 JSON 字符串：

```csharp
string json = DataMaster.Instance.GetParamValueJson(topicId, paramName);
```

获取并反序列化为目标类型：

```csharp
T value = DataMaster.Instance.GetParamValue<T>(topicId, paramName, fallback);
```

读取规则：

- Editor 中 `EffectiveValue` 使用 `default_value`。
- 非 Editor 中优先使用服务端 `new_value`，没有服务端值时使用本地 `default_value`。
- 读取时会解密本地缓存值。
- 解密失败会认为数据可能被篡改，重建本地数据库并返回 `null` 或 `fallback`。
- JSON 反序列化失败会打印错误并返回 `fallback`。

业务示例：

```csharp
private const string TopicId = "fd83cd8a";
private const string ParamName = "logic";

NotificationTopicConfig config =
    DataMaster.Instance.GetParamValue<NotificationTopicConfig>(TopicId, ParamName);
```

带兜底值示例：

```csharp
int reviveCost = DataMaster.Instance.GetParamValue<int>(
    "topic_kgkB6V",
    "revive_cost",
    fallback: 100);
```

建议：

- 业务侧按 topic/param 封装 loader，不要在 UI 或系统逻辑里散落字符串。
- 对复杂 config 类型使用 `JsonProperty` 标记字段名。
- 对读取结果做空值保护；DataMaster 只保证返回默认值或服务端值，不保证业务结构一定完整。

## 7. 事件上报

入口：

```csharp
DataMaster.Instance.LogEvent(eventName, value, userContext);
```

参数说明：

- `eventName`：事件名。
- `value`：主数值，写入 `primaryValue`。
- `userContext`：用户上下文，类型为 `DMUserContext`。

`DMUserContext` 支持字段：

- `PlayerId`
- `DeviceId`
- `AdId`
- `MediaSource`
- `CampaignName`
- `AdCreativeName`
- `InstallChannel`
- `InstallTimeMs`
- `CountryCode`
- `LanguageCode`
- `AppVersion`
- `OsVersion`
- `DeviceModel`
- `NetworkType`
- `TimezoneName`
- `SessionId`
- `ExtraContext`

`// 策划需要什么数据属性就添加什么属性`

上报流程：

1. 检查是否已初始化。
2. 递增本地事件序号 `_dmSeq`。
3. 构造 `ReportDMBatchRequest`，当前每次只上报一个 `DMEventItem`。
4. 设置 `EventId`、`EventTimeMs`、`Seq`。
5. JSON 序列化、GZip 压缩、AES 加密。
6. POST 到 `EventUrl`。
7. 成功后保存 `_dmSeq` 到 `PlayerPrefs` 的 `DM_SEQ_CACHE`。

示例：

```csharp
var userContext = new DMUserContext
{
    PlayerId = playerId,
    DeviceId = deviceId,
    CountryCode = countryCode,
    AppVersion = appVersion,
    ExtraContext = new Dictionary<string, object>
    {
        ["level"] = currentLevel,
        ["source"] = "revive",
        // 策划需要什么数据就上传什么数据
    }
};

DataMaster.Instance.LogEvent("revive_ad_show", 1, userContext);
```

注意：

- 当前实现没有上报失败重试队列。
- `_dmSeq` 只在上报成功后持久化；失败时本次递增不会写入 `PlayerPrefs`。
- 上报时间使用服务端时间偏移；拉取配置响应头 `x-server-time` 会校准本地 `_serverTimeOffset`。

## 8. 调试接口

以下接口仅在 `DEVELOPMENT_BUILD` 或 `UNITY_EDITOR` 中可用：

```csharp
string info = DataMaster.Instance.DebugGetParamInfo(topicId, paramName);
string topicInfo = DataMaster.Instance.DebugGetTopicInfo(topicId);
string allInfo = DataMaster.Instance.DebugGetAllTopicsInfo();
```

用途：

- 查看指定参数的 `Default`、`NewValue` 和 `Effective`。
- 查看指定 topic 下所有参数。
- 查看本地库中所有 topic。

注意：

- 调试接口会输出明文配置值，只用于开发和排查问题。
- 不要在正式埋点、日志或 UI 中暴露这些输出。

## 9. 本地存储与安全

本地数据库：

- 路径：`Application.persistentDataPath/dm_param_table.db`
- 参数表：`dm_param_table`
- 实验表：`dm_experiment_table`

参数值存储：

- `default_value`：本地默认配置值，AES 加密后的 Base64 字符串。
- `new_value`：服务端下发值，AES 加密后的 Base64 字符串。
- `EffectiveValue`：运行时读取的生效值。

篡改处理：

- 本地值解密失败时，SDK 会调用 `ResetLocalDatabase`。
- `ResetLocalDatabase` 会 drop 参数表和实验表，再用 `_defaultConfig` 重建。

## 10. 接入顺序建议

推荐启动流程：

1. 加载本地默认配置 `TextAsset`。
2. `ParseConfigJson` 解析默认配置。
3. `Initialize` 初始化 DataMaster。
4. 业务模块先读取默认配置启动。
5. 用户登录完成后调用 `RefreshFromServer`。
6. 拉取成功后广播配置更新事件。
7. 业务 loader 清缓存并重新读取配置。
8. 玩家实际进入实验影响范围时调用 `SetTopicExposureTimeMs`。
9. 需要 DataMaster 事件时调用 `LogEvent`。

## 11. 常见问题

### 拉取没有任何效果

检查：

- 是否先调用了 `Initialize`。
- `userId` 是否为空。
- `onError` 是否返回网络、解密或 JSON 错误。
- 服务端返回的 topic/param 是否与本地 `schema` 和业务读取的字符串一致。

### 获取配置返回 null 或 fallback

检查：

- `topicId` 和 `paramName` 是否正确。
- 默认配置中是否存在该 topic 的 `schema`。
- 参数 JSON 是否能反序列化为目标类型。
- Editor 下读取的是默认值，非 Editor 下才优先读取服务端值。

### 正式环境请求到了测试域名

检查构建是否定义了 `PRODUCTION_PACKAGE`。未定义时会使用 dev 域名。
