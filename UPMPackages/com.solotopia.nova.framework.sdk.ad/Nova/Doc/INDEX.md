# Nova Framework - SDK - Ad 文档索引

> 本包为 Nova 框架广告聚合插件基类，调度格式来自 `AdFormat`：Rewarded / Interstitial / Banner / AppOpen / Native；实际支持哪些格式由渠道注册与实现决定，当前 MAX 与独立 AdMob 实现前四种，内置打点。
> 具体渠道（MAX、AdMob 等）继承 AdChannelPluginBase 实现各自逻辑；业务侧通过 IAdPlugin 接口调度，与渠道解耦。非基类自定义渠道须自行实现回调线程、取消和占用边界，见[聚合文档](./AdPlugin.md#取消令牌的作用范围)。

---

## 业务侧公开 API

| 类型 | 说明 | 文档 |
|---|---|---|
| `IAdPlugin` | 广告聚合插件公开接口，定义业务侧可调用的所有广告操作 | [IAdPlugin.md](./IAdPlugin.md) |
| `AdPlugin` | 广告聚合调度插件，实现 `IAdPlugin`，管理多渠道切换与降级 | [AdPlugin.md](./AdPlugin.md) |
| `IBannerControl` | Banner 广告控制接口，提供 Show / Hide / Destroy 等精细控制入口 | [IBannerControl.md](./IBannerControl.md) |
| `AdChannelPluginBase` | 广告渠道插件抽象基类，各渠道包继承此类实现具体适配 | [AdChannelPluginBase.md](./AdChannelPluginBase.md) |
| `AdPluginConfig` | 插件序列化配置，承载广告组、开关、超时等参数 | [AdPluginConfig.md](./AdPluginConfig.md) |
| `AdPluginEvents` | 广告事件容器，汇聚初始化、加载、展示、收益与关闭通知 | [AdPluginEvents.md](./AdPluginEvents.md) |
| `AdRequestReason` | 广告请求原因枚举，区分默认请求、手动请求、重试和展示后补量 | [AdRequestReason.md](./AdRequestReason.md) |
| `AdRevenueReportNetService` | 收益协议的参数、路由校验与网络调用；由渠道基类驱动，业务收益订阅不重复发送 | [收益上报](#广告收益上报-proto-协议层) |

基类广告组默认组间并行、组内串行；MAX 保留源表达式拓扑，当前 AdMob 经真实 ID 访问器展开后注册独立单 ID 组，详见[配置文档](./AdPluginConfig.md)。`AdGroupConfigParser` 将 List 每项解析为一组，项内用英文逗号或竖线分隔；单 ID 项自然成为单 ID 组。统一循环重试间隔见 [AdPluginConfig.md](./AdPluginConfig.md)，渠道完整接口、状态与接入说明见 [AdChannelPluginBase.md](./AdChannelPluginBase.md)，请求、展示关键节点及展示后补量（`AutoRefill`）和自动重试（`Retry`）的新轮日志见 [AdPlugin.md](./AdPlugin.md#本地请求与展示日志) 和[基类日志说明](./AdChannelPluginBase.md#本地流程日志)。

Banner 首次显示选择、固定显示归属及 `AdPlugin.TryShowBanner()` 的失败反馈见 [AdPlugin](./AdPlugin.md)。MAX / AdMob 的 Ready 与收益只查询实际控制的首个 Banner ID，扩展入口见 [渠道基类](./AdChannelPluginBase.md)。

<a id="广告收益上报-proto-协议层"></a>

## 广告收益上报 Proto（协议层）

广告包提供 [Nova/Protos/pb_net_iaa_report.proto](../Protos/pb_net_iaa_report.proto) 作为 广告收益上报的唯一协议源，并由 [Nova/Scripts/Runtime/Protos/PbNetIaaReport.cs](../Scripts/Runtime/Protos/PbNetIaaReport.cs) 提供生成类型。协议包含 `PbIAAReportData`、`PbNetIAAReportReq` 和 `PbNetIAAReportResp`，接口注释为 `POST /v1/taijiv2/report`；请求通过 `ad_channel` 记录渠道，IAA 业务数据放在 `iaa` 嵌套消息中，响应 `iaa_report_id` 回显业务 ID，`error_code` 保留字段号 `100`。请求和响应复用框架公共 `pb_net_header.proto` 的 `PbNetReqHeader` / `PbNetRespHeader`，不在本包重复定义 Header。

运行时协议由 `AdChannelPluginBase.StartRevenueReport` 统一承接：配置 `AdPluginConfig.RevenueReportCmdName=AdRevenueReport` 后，MAX 在 `TrackMaxAdIlrd` 派发时显式调用，Banner 共用 ILRD 的同一份累计和存档。AdMob 在原厂 `Paid` 入口显式接入同一渠道累计。`RaiseRevenue` / `RaiseRevenueImmediately` 只发布逐次业务通知。基类通过 [Nova/Scripts/Runtime/Services/AdRevenueReportNetService.cs](../Scripts/Runtime/Services/AdRevenueReportNetService.cs) 构造并发送 `PbNetIAAReportReq`。NetCmd/Host 由消费项目配置，旧资产字段为空时不上报；自定义派生渠道须显式接入协议入口。

### 收益回调、打点与后台请求的分工

| 路径 | 当前节奏与边界 |
|---|---|
| MAX 原始收益通知 | 每笔收益都通知业务，Banner 通知携带当笔金额；不在业务事件订阅中重复调用收益协议 |
| MAX `ad_impression` | 每笔收益尝试派发，不随 Banner 阈值节流 |
| MAX 非 Banner `ad_ilrd` 与后台请求 | 每笔收益尝试派发 ILRD；至少向一个目标尝试调用后触发一次后台请求。所有目标不可用时不发协议，也不缓存这笔全屏收益用于补发 |
| MAX Banner `ad_ilrd` 与后台请求 | 同渠道全部 Banner 共用一份次数、金额和存档，默认累计 5 次；达到阈值且 ILRD 已尝试派发后，以同一累计金额触发一次协议。无可用目标时保留批次，后续收益到来再尝试 |
| AdMob | 当前没有独立分析 `ad_ilrd`。`Paid → TrackAdMobRevenue` 显式接入协议；Banner 复用共享渠道累计，非 Banner 按次发送 |

Banner 分析载荷使用最后一次回调的广告位与网络信息；后台协议只传最后一次广告位及累计 `price_usd`，不携带网络信息或这批真实曝光次数。批次清理按“已尝试派发”执行，不等待分析平台或收益服务器确认。网络失败只记录警告，不创建第二份累计或持久网络补发队列；完整存储与失败语义见 [渠道基类](./AdChannelPluginBase.md#广告收益上报)。

`RevenueReportCmdName` 为空只停用后台协议，不停用 MAX 的分析事件或原始收益通知，也不会冻结 Banner 批次。AdMob 的 Banner 累计回调在调用 `StartRevenueReport` 后即返回已派发，即使路由为空或网络失败也按该入口的规则清批；不能依赖稍后补填路由来补报旧收益。MAX 同样在 ILRD 已尝试派发后清批，不等待后台协议启用或成功。

### Service 入口与失败边界

`AdRevenueReportNetService` 位于 `NovaFramework.SDK.AdPlugin.Runtime`，当前公开签名为：

```csharp
public UniTask<NetResponse<PbNetIAAReportResp>> Async(string cmdName, AdEvent revenue, string reportId, AdChannelType channel, CancellationToken cancellationToken = default);
```

该入口允许零美元收益；空载荷、空 `reportId`、空广告位、非 USD、负数或非有限金额返回 `PARAM_ERROR`，Network Manager 未就绪或找不到 NetCmd 返回 `URL_NOT_FOUND`。参数和路由错误不发送 HTTP。调用方取消向上传播，其他网络结果沿用 `NetService`；渠道基类负责隔离异常和在释放时取消请求。直接调用 Service 不会生成 ID、累计 Banner、持久补发或向业务事件发布结果，日常业务使用聚合接口即可。

### 请求与响应字段

| 消息 | 字段号与含义 |
|---|---|
| `PbNetIAAReportReq` | `head=1`：框架公共身份头；`ad_channel=2`：渠道枚举字符串；`iaa=3`：本笔或本批收益 |
| `PbIAAReportData` | `report_id=1`：业务 ID；`ad_type=2`：格式枚举字符串；`ad_id=3`：真实广告位；`price_usd=4`：美元 double，允许 0，拒绝负数和非有限值 |
| `PbNetIAAReportResp` | `head=1`；`iaa_report_id=2`：回显业务 ID；`utc_day=3`：服务端受理的 UTC 日期；`duplicate=4`：当日重复计数标志；`error_code=100`：旧 Tiny App 兼容业务码 |

客户端公共头的字段名为 `appid`、`uid`、`version`、`language`、`devid`、`platform`、`channel`、`openid`，字段号以框架 `pb_net_header.proto` 为准；HTTP 头中的 `app_id` 是另一个传输字段。渠道与广告格式直接使用枚举 `ToString()`，不额外维护 switch 映射。

一次新的收益派发在基类生成一个 `Guid.NewGuid().ToString("N")` 作为 report_id。同一请求若发生传输候选切换，复用已经编码的请求及 ID；主动重新发起一笔收益会生成新 ID。`duplicate=true` 是当日幂等结果，不作为失败；UTC 日和去重结论由服务端决定，客户端不自行认定已经入账。

### 实际网络发送方式

`AdRevenueReportNetService` 将字段交给框架网络层，实际流程为：

1. 通过 NetCmd / Host 解析 URL；NetCmd 建议为 `AdRevenueReport`，协议路径为 `POST /v1/taijiv2/report`。
2. `PbNetIAAReportReq` 序列化为 Protobuf 字节，使用当前 `AppConfigs.AppAesKey / AppAesIV` 做 AES-128-CBC / PKCS7 加密，两者均须为 UTF-8 16 字节。
3. 以 `Content-Type: application/octet-stream` 发送原始密文；额外请求头为 `app_id` 与 `Encoding-Aes`，后者是 IV 的 UTF-8 字节经 Base64 编码后的值。
4. HTTP 成功响应使用同一 Key / IV 解密，先解析 `PbNetBaseResponse.code / message / data`，再解析 data 中的 `PbNetIAAReportResp`；外层业务码与响应体的 `error_code` 分开判断。

日志中的 JSON 用于查看业务参数，不是 HTTP 请求体。Key / IV 不使用隐私配置的默认密钥，也不在广告包内硬编码；配置、取消、主备和错误码边界沿用 [NetService](../../README.md#跨包文档定位)（`com.solotopia.nova.framework` 包内 `Docs/Runtime/Modules/Network/NetService.md`）。

### 收益日志排查

| 阶段 | 实际日志及判断方法 |
|---|---|
| 配置生效 | Info：`广告收益上报配置` 或 `广告收益上报未启用`；只能证明当前渠道拿到的 NetCmd，不能证明已经发出请求 |
| 进入协议入口 | Debug：`收到广告收益`；Banner 只有到了派发入口才出现这条，原厂每笔收益日志与它的条数不应直接比较 |
| 参数与路由通过 | Debug：`广告收益上报参数`，包含命令、渠道、report_id、类型、广告位和金额；当前没有单独的 Info“准备发送”日志 |
| 网络返回 | Debug：`广告收益上报网络结果`，包含外层状态、错误和完整业务体；`iaa_report_id / utc_day / duplicate` 从 data 核对 |
| 参数、路由或发送失败 | Warning：参数无效、跳过原因、网络失败或响应业务错误；正常渠道释放取消使用 Debug |

详细日志依赖 Nova 的 Debug 日志开关和 Player 编译配置，MAX 原厂日志开关不能替代它。Protobuf JSON 会省略 false、0 和空字符串等默认值；不能把未出现 `duplicate` 或 `error_code` 误判为字段未解析。没有收益协议日志时先核对路由和 Debug 开关，再区分 Banner 未满批次、ILRD 无可用目标、渠道已释放和网络失败。

## 相关

- [AdPlugin.md](./AdPlugin.md) — 广告聚合调度插件
- [AdPluginConfig.md](./AdPluginConfig.md) — 广告聚合配置
- [AdChannelPluginBase.md](./AdChannelPluginBase.md) — 渠道插件抽象基类

## SDK 初始化与账号规则

正常生命周期由 SDKManager 每轮创建新聚合与新渠道，已释放 AdPlugin 不支持同对象再次初始化；新一轮业务重新获取实例并建立订阅，组件 InitializeTask 的单次缓存不提供重启。初始化 ct 仅关联尚未结束的渠道准备与初始化，成功后不长期控制其事件和业务请求。

本插件由 ConfigMaster 启用；聚合插件就绪表示渠道已创建，不代表全部原厂初始化成功，`InitResult` 是单渠道结果。初始化是否等待其他 SDK 由插件的 `ISDKInitializationDependencies` 能力声明决定，`Priority` 不控制启动先后；业务只需目标能力时使用 `Nova.SDK.WaitForPluginAsync<T>()` 并检查 `Ready`。游戏后端登录成功后调用 `Nova.SDK.Login(uid, userProperties)`，框架会在插件就绪后补交最新账号；切换账号再次调用，登出调用 `EndLoginSession()`。新接入 SDK 按框架 SDK 插件契约实现。现有 `IAdChannelConfig` 保持真实 ID 访问器，具体配置类另外提供组表达式。

广告初始化不自动请求 Rewarded、Interstitial、AppOpen 或创建 Banner；由业务登录、隐私与资源准备流程决定何时显式调用 `RequestAsync`。SDK 账号分发与网络请求身份是不同职责：协议公共头读取 GameLogin 等业务流程写回的 `NetService` 身份，调用 `Nova.SDK.Login` 不代替后端登录。AdDemo 的登录按钮和操作门禁见 [Sample README](../../README.md#跨包文档定位)（`com.solotopia.nova.framework.sdk.ad` 包内 `Sample AdDemo / README.md#先登录再操作广告`），广告基类本身不负责发起账号登录。
