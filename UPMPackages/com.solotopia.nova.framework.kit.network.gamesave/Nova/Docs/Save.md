# Save

## 1. 简介

`Save` 是游戏存档（云存档）业务网络 Service，封装存档节点在客户端与服务端之间的获取与上传链路。

**所在文件：** `Nova/Scripts/Runtime/Save.cs`
**命名空间：** `NovaFramework.Kit.Network.GameSave.Runtime`
**类签名：** `public sealed class Save`

> 通过 `Nova.Network.Kit<Save>()` 获取实例，不继承任何基类，无参构造即可使用。
>
> **边界提示：** 本类仅负责云存档传输；本机持久化由主框架 `PersistManager` / `FileFragmentManager` / `SQLiteManager` 负责，二者职责不重叠。

---

## 2. 公开 API

### 前置配置

`Save` 所有接口的 `cmdName` 由 `SaveKitConfig` 统一提供，需在 ConfigWindow「Kit 配置」中填写：

| 配置字段 | 说明 |
|---|---|
| `GetCmdName` | 获取存档协议的 NetCmd 指令名（如 `GameSaveGet`），`GetAsync` / `GetFullAsync` 共用 |
| `SetCmdName` | 上传存档协议的 NetCmd 指令名（如 `GameSaveSet`），`SetAsync` / `SetFullAsync` 共用 |

> 若 `SaveKitConfig` 未在 ConfigWindow 启用，调用任何入口将抛出 `KitConfigMissingException`（开发期 fail-fast）。

### 方法

> **全量 vs 非全量：** 仅 `GetFullAsync` / `SetFullAsync` 走全量（请求体 `full=true`），其余接口一律为非全量（`full=false`）。`PbNetGetGameDataReq.full=true` 时服务端忽略 `keys` 拉取该用户全部存档；`PbNetSetGameDataReq.full=true` 时服务端用 `datas` 整体替换该用户存档。

#### 调试 / 元数据

| 签名 | 说明 |
|---|---|
| `public void SetDebugMode(bool debugMode)` | 设置本实例调试模式覆盖；仅影响本实例发出的请求；`false` 时不等于关闭全局，仅取消覆盖 |
| `public void SetGameVersion(string gameVersion)` | 设置本实例的游戏存档版本号；后续所有 `SetAsync` / `SetFullAsync` 请求自动写入 `GameVersion` 字段；传 `null` 按空串处理；未调用前默认空串 |

#### 拉取（非全量）

| 签名 | 说明 |
|---|---|
| `public UniTask<NetResponse<PbNetGetGameDataResp>> GetAsync(string key)` | 单 key 拉取；cmdName 取自 `SaveKitConfig.GetCmdName` |
| `public UniTask<NetResponse<PbNetGetGameDataResp>> GetAsync(string[] keys)` | 批量 key 拉取；cmdName 取自 `SaveKitConfig.GetCmdName` |

> **入参校验：** 非全量 `GetAsync` 要求 `key` / `keys` 必传且非空 —— `key` 不得为 `null` / `""`；`keys` 不得为 `null` / 长度 0，且任一元素不得为 `null` / `""`。校验失败直接返回 `NetErrorCode.PARAM_ERROR`，不发起网络请求。

#### 拉取（全量）

| 签名 | 说明 |
|---|---|
| `public UniTask<NetResponse<PbNetGetGameDataResp>> GetFullAsync()` | 全量拉取；cmdName 取自 `SaveKitConfig.GetCmdName`；请求体仅写 `Full=true`，不携带 keys |

#### 上传（非全量）

| 签名 | 说明 |
|---|---|
| `public UniTask<NetResponse<PbNetSetGameDataResp>> SetAsync(string key, string value)` | 单条上传；cmdName 取自 `SaveKitConfig.SetCmdName` |
| `public UniTask<NetResponse<PbNetSetGameDataResp>> SetAsync(string[] keys, string[] values)` | 批量上传；cmdName 取自 `SaveKitConfig.SetCmdName` |

> **入参校验：** 非全量 `SetAsync` 要求 `keys` / `values` 必传且非空 —— `keys` 不得为 `null` / 长度 0，任一元素不得为 `null` / `""`；`values` 不得为 `null`、长度必须与 `keys` 一致，任一元素不得为 `null`（允许空字符串）。校验失败直接返回 `NetErrorCode.PARAM_ERROR`，不发起网络请求。

#### 上传（全量）

| 签名 | 说明 |
|---|---|
| `public UniTask<NetResponse<PbNetSetGameDataResp>> SetFullAsync(string value)` | 全量上传；cmdName 取自 `SaveKitConfig.SetCmdName`；请求体写 `Full=true`，`value` 作为整包载荷塞进 `datas[0].Value`（`Key` 为空字符串）；`value` 不得为 `null`（允许空字符串），否则返回 `NetErrorCode.PARAM_ERROR` |

### 协议数据结构

| 类型 | 说明 |
|---|---|
| `PbNetGameDataNode { string Key; string Value }` | 通用键值节点；Key 为小表名称，Value 为业务 JSON 序列化后的字符串 |
| `PbNetGetGameDataReq { PbNetReqHeader Head; bool Full; repeated string Keys }` | 拉取请求；`Full=true` 时服务端忽略 `Keys` 返回全部存档 |
| `PbNetGetGameDataResp { PbNetRespHeader Head; string GameVersion; string AppVersion; string LastDeviceId; int64 LastTimestamp; repeated PbNetGameDataNode Datas }` | 拉取响应；附带服务端记录的最近一次存档元数据（游戏存档版本号 / 应用版本号 / 设备 ID / 时间戳，毫秒级 unix 时间戳） |
| `PbNetSetGameDataReq { PbNetReqHeader Head; bool Full; string GameVersion; string AppVersion; string LastDeviceId; repeated PbNetGameDataNode Datas }` | 上传请求；`Full=true` 时服务端用 `Datas` 整体替换该用户存档；`GameVersion` 由业务侧通过 `SetGameVersion` 注入，`AppVersion` 自动取 `Application.version`，`LastDeviceId` 自动从 `IDeviceIdProvider` 取值 |
| `PbNetSetGameDataResp { PbNetRespHeader Head; int32 Effect }` | 上传响应；`Effect` 为服务端写入的影响条数 |

---

## 3. 使用示例

> **前提：** 在 ConfigWindow「Kit 配置」中启用 `SaveKitConfig`，填写 `GetCmdName`（如 `GameSaveGet`）和 `SetCmdName`（如 `GameSaveSet`）。

```csharp
// 获取 Save Service 实例
var gameSave = Nova.Network.Kit<Save>();

// 切换为调试模式（仅影响本实例发出的请求）
gameSave.SetDebugMode(true);

// 注入游戏存档版本号（登录/初始化阶段调一次即可，后续 Set/SetFull 自动写入）
gameSave.SetGameVersion("1.0.0");

// 1. 全量拉取（仅 GetFullAsync 走 full=true 分支）
var allResp = await gameSave.GetFullAsync();
if (allResp.IsSuccess)
{
    foreach (var node in allResp.Data.Datas)
    {
        // node.Key / node.Value 业务侧自行 Util.Json.Parse 解析
    }
}

// 2. 单 key 拉取（非全量）
var oneResp = await gameSave.GetAsync("Bag");
string bagJson = oneResp.IsSuccess && oneResp.Data.Datas.Count > 0
    ? oneResp.Data.Datas[0].Value
    : string.Empty;

// 3. 批量 key 拉取（非全量）
var manyResp = await gameSave.GetAsync(new[] { "Bag", "Quest", "Settings" });

// 4. 单条上传（非全量增量）
var setOneResp = await gameSave.SetAsync("Bag", bagJson);

// 5. 批量上传（非全量增量）
var keys   = new[] { "Bag", "Quest" };
var values = new[] { bagJson, questJson };
var setManyResp = await gameSave.SetAsync(keys, values);

// 6. 全量上传（仅 SetFullAsync 走 full=true 分支；value 作为整包载荷写入 datas[0].Value，Key 为空字符串）
string fullPayload = "{\"Bag\":\"...\",\"Quest\":\"...\"}";
var setFullResp = await gameSave.SetFullAsync(fullPayload);
```

---

## 4. 内部约束

- **`SetDebugMode` 覆盖语义**：`m_DebugModeOverride` 为 `bool?`，调用 `SetDebugMode(true/false)` 后会覆盖全局 `NetService.IsDebugMode`；若需恢复跟随全局，暂无公开 API，需重新 `Kit<Save>()` 获取新实例。
- **`Head` 自动填充**：所有 `Async` 入口内部调用 `NetBuilder.BuildHeader()` 填充请求 Head，业务侧无需手动构建。
- **`Set` 请求元数据自动填充**：`SetAsync` / `SetFullAsync` 内部自动写入三字段 —— `GameVersion` 取自 `m_GameVersion`（业务侧 `SetGameVersion` 注入，未注入则为空串），`AppVersion` 取自 `Application.version`，`LastDeviceId` 通过 `Nova.SDK.TryGet<IDeviceIdProvider>()` 取值（未注册则为空串）。取值口径与 `NetBuilder.BuildHeader` 保持一致。
- **`full` 由接口决定，禁靠 keys 空判**：是否全量完全由具体接口决定 —— 仅 `GetFullAsync` / `SetFullAsync` 写入 `full=true`；`GetFullAsync` 不携带 keys；`SetFullAsync` 将 `value` 作为整包载荷写入 `datas[0].Value`（`Key` 为空字符串）。其余接口一律 `full=false`。**已废弃旧版"keys 为空 = 全量"的隐式回退**。
- **非全量入参强校验**：非全量 `GetAsync` / `SetAsync` 在 `cmdRow == null` 之前先校验业务参数 —— `key` / `keys` 不得为 `null`/`""`/长度 0；`values` 不得为 `null`、长度必须与 `keys` 一致、任一元素不得为 `null`（允许空字符串）。任一不通过直接返回 `NetErrorCode.PARAM_ERROR`，不发起网络请求。
- **`cmdName` 由 `SaveKitConfig` 提供**：所有入口内部通过 `Nova.Config.GetKitConfig<SaveKitConfig>()` 取配置，取不到时抛 `KitConfigMissingException`；未注册的 cmdName 会落到 `null` `INetworkCmdRow`，由下层 `NetService.SendAsync` 统一处理。
- **value 透传**：`Value` 字段是业务 JSON 序列化后的字符串；框架不解析具体业务 schema，业务侧自行 `Util.Json.Parse<T>(value)`。
- **resp Head 元数据**：`resp.Data.Head.Uid` / `resp.Data.Head.AppId` 等响应公共头字段由业务侧按需自取，框架不做校验或写回。
- **依赖主框架公共网络编排层**：`NetService.SendAsync` / `NetBuilder.BuildHeader` / `NetResponse<T>` / `INetworkCmdRow` 均来自主框架包 `com.solotopia.nova.framework` 的 Network Kit 公共层。

---

## 5. 关联

- 同包：[SaveErrorCode.md](./SaveErrorCode.md) — 游戏存档业务段错误码
- 同包：[SaveKitConfig.md](./SaveKitConfig.md) — 云存档 Kit 配置
