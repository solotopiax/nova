# IAP 商品目录协议

`NovaFramework.SDK.IAP.Editor.IAPProductCatalogProtocol` 位于 IAP 父包的 Editor 程序集中。它从已导出的 `ConfigRuntimeSO` 创建商品目录快照，供 IAP 配置 Inspector、Pipify 和受控目录上传 Action 复用同一投影、JCS、App AES 与发送实现。

## 上传地址

地址固定在协议类中，由当前 `ConfigRuntimeSO.DevelopMode` 选择：

| 模式 | 地址 |
|---|---|
| Debug / `develop_mode="Debug"` | `https://t-tools-admin.ahameet.com/api/admin/v1/iap/product-catalogs/upload` |
| Release / `develop_mode="Release"` | `https://tools-admin.ahameet.com/api/admin/v1/iap/product-catalogs/upload` |

`DevelopmentUploadUri`、`ProductionUploadUri` 和 `GetUploadUri(DevelopMode)` 均为静态入口。`CreateFromCurrentConfig()` 返回的快照也保存本次选择的 `UploadUri`。

## 请求

```json
{
  "head": {
    "app_id": 12345,
    "platform": "Android",
    "channel": "Google",
    "develop_mode": "Debug",
    "version": "1.2.0"
  },
  "body": "<Base64 AES ciphertext>"
}
```

外层 `head` 保持明文。`body` 密文对应的完整 JSON 明文为：

```json
{
  "catalog_hash": "900928b2f6daa5c2f45ce2a7b89d890f20ac34917e90e9c6a81771ba637ffa8b",
  "product_count": 1,
  "products": [
    {
      "table_id": "1001",
      "name": "金币包",
      "product_id": "com.nova.coins100",
      "third_product_id": "coins100",
      "product_type": "Consumable",
      "sub_group_id": 0,
      "price": "0.99",
      "currency": "USD"
    }
  ]
}
```

外层请求固定只有 `head` 和 `body` 两个字段。线上 `body` 的 JSON 类型是字符串，值为 AES 密文字节的 Base64 表示；不能把 Body 明文对象直接放进外层请求。Head 固定只有 `app_id`、`platform`、`channel`、`develop_mode`、`version`，不发送 `environment` 和 `package_id`。

商品来自 `IAPPluginConfig.Products`，按数值 `TableId` 升序冻结。字符串保留配置原值，`null` 输出为空字符串；重复 SKU 允许，非正或重复 TableId、空商品表、null 行和未定义商品类型会阻止生成。

`catalog_hash` 只覆盖按 RFC 8785/JCS 规范化后的八字段明文 `products` 数组，以 UTF-8 无 BOM 字节计算 SHA-256；Head、AES 密钥、IV 和上传地址不参与 Hash。

Body 按 `catalog_hash → product_count → products` 的字段顺序紧凑序列化，再使用 AES-128-CBC、PKCS7 加密并输出 Base64。Key 与 IV 分别来自同一份导出物的 `ConfigRuntimeSO.AppConfigs.AppAesKey/AppAesIV`，按 UTF-8 编码必须各为 16 字节；协议始终显式传入这组值，不会回退到 `PrivacyConfigs` 注入的默认 AES。配置缺失或长度错误会在发送前阻止上传。

## 上传前置条件

手动按钮和 Pipify Step 使用同一组校验：

- 当前激活的 `ConfigRuntimeSO` 已正式导出，并包含 `AppConfigs`。
- `AppID` 是正 int32，当前 Unity Active BuildTarget 与导出平台一致。
- 平台属于 Android / iOS / WebGL，渠道属于 Official / Google / Apple / WeChat / TikTok / Alipay。
- `DevelopMode` 是 Debug 或 Release，`PlayerSettings.bundleVersion` 非空。
- `AppAesKey` 与 `AppAesIV` 按 UTF-8 编码分别为 16 字节。
- 已启用的 `IAPPluginConfig` 包含至少一条合法商品。

任一条件不满足时，请求不会发送。上传实现不会在日志中输出 App AES Key、IV 或 Body 明文。

```csharp
IAPProductCatalogSnapshot snapshot = IAPProductCatalogProtocol.CreateFromCurrentConfig();
string json = IAPProductCatalogProtocol.Serialize(snapshot.Request);
```

旧入口 `IAPProductCatalogUploader.UploadCurrentAsync` 读取当前激活且已导出的 `ConfigRuntimeSO`，构造并序列化上述快照，再以 HTTP POST 上传到 `snapshot.UploadUri`。请求使用 UTF-8 JSON、空额外 Header、30 秒超时且不自动重试；配置校验、网络、超时、取消或其他非 2xx 响应仍抛出异常。仅当 HTTP 409 的 JSON 响应码准确为 `CATALOG_VERSION_CONFLICT` 时，保持原包含服务端响应的 Warning 并正常结束；本次新目录未应用。处理冲突时先核对目录及版本策略，不能为消除告警自动改版本或重传。其他 409 仍是失败。

IAP 配置的商品列表在“导入 SKU Excel”下方提供“上传支付表到服务器”按钮。按钮上传期间不可重复点击，并调用同一个 `IAPProductCatalogUploader.UploadCurrentAsync`；成功时写入地址与响应正文，失败时显示 Editor 对话框。

## 受控一次提交 Action

`nova.project.iap.upload-product-catalog` 是 Framework 核心注册的 Delivery Action，经 MCP 原 `nova_project_action` 公开；IAP 为可选包，Framework 不增加对其程序集的强依赖。没有匹配包或固定桥时不上传；不接受任意类型、方法、地址或 Body。使用前由当前 Registry `describe` 核对真实可用性与严格 Schema。

```json
{
  "runtimeGuid": "<现存ConfigRuntimeSO的N格式GUID>",
  "developMode": "Debug"
}
```

两个字段均必填，模式只接受精确 `Debug` / `Release`。稳定 Edit Mode 下，Plan 只读定位持久化激活 Master 及其显式、已保存的 `ExportTarget`，要求导出坐标、活动平台与请求环境相符，拒绝 dirty 对象；不调用会修复 pathHint 的当前配置入口，不导出或保存配置，不发 HTTP。

冻结范围是 Master、Master meta、Runtime、Runtime meta、`ProjectSettings/Nova/Globals.json`、`ProjectSettings/ProjectSettings.asset` 六文件及 Master/Runtime 内存摘要、活动目标、应用身份、`PlayerSettings.bundleVersion`、固定上传地址、完整规范目录与最终序列化请求、固定桥和代码身份。最终请求只保留在原 Prepared 内存中，Plan 对外仅给目录 Hash/数量及请求 Hash/字节数等脱敏元数据。真实提交须已获针对环境、目标、客户端版本、目录摘要和至多一次请求的精确授权，再绑定原一次性计划确认令牌。

Execute 在重读输入保持后，于 `Library/Nova/AgentActions/IapCatalogUploads/<attemptId>.json` 独占写入并读回正式发送意图；失败则零 HTTP。随后只提交原冻结请求，Inspector、Pipify 与 Action 共用发送门，拒绝重入而不排队。同 Prepared 的任何一次 Submit 尝试（包括预取消、busy 和失败）后均不能再次发送。

跨可选包的固定 Editor 桥为 `NovaFramework.SDK.IAP.Editor.IAPProductCatalogActionBridge`，仅提供三项公开静态方法：

| 方法 | 输入与输出 |
|---|---|
| `Prepare(ConfigRuntimeSO) → object` | 从显式导出物创建桥私有 Prepared，不读取全局当前配置、不保存或发送 |
| `Describe(object) → string` | 返回 schemaVersion、AppID/坐标/环境/版本、固定地址、catalogHash/productCount、requestSha256/requestByteCount；不暴露 Body |
| `SubmitAsync(object, CancellationToken) → Task<string>` | 接受同桥原 Prepared，内部复用 UniTask 一次发送核心，仅返回有限脱敏结果 |

结果包含 `outcome/dispatched/statusCode`、响应字节数/SHA、`versionConflict`、有限 `exceptionKind`、起止时间和请求 SHA；不包含密钥、明文或密文 Body、原响应或异常正文。HTTP 2xx 记录为 `http-accepted`，只证明 HTTP 接受；当前没有权威后台目录应用响应或查询契约，Action 与 Verify 均最高 `partial`。精确版本冲突为 `version-conflict + remoteApplication=not-applied`；非精确 409、其他错误、取消或发送结果未知均不自动重发。

Verify 使用原 Core `recovery_token`，只读原 attempt 记录、身份与结果，不 Prepare、不上传、不查询后台。重载后原计划不能 Execute；只有 `dispatching` 意图、记录缺失/损坏或发送后结果未落盘时保持远端状态未知，不据此认定未发送。正式意图、结果及 Operation 必须保留，不作为临时日志清除。

此 Action 只提交目录，不创建订单、验单、恢复购买或发货，也不表示 `sdk-kit.iap.business` 的设备与真实支付链通过。无网络的假发送器测试只证明冻结、次数、结果适配和恢复合同，不是后台验收。

## Pipify

安装 IAP 父包后，Pipify 会通过 TypeCache 自动发现以下步骤，不需要 Framework 增加 IAP 依赖：

| StepId | 分类 | 显示名 |
|---|---|---|
| `iap.product_catalog.upload` | `IAP` | 导出IAP数据到服务器 |

该步骤没有参数配置项，也不会显示参数配置区域。执行时会把 `PipifyContext.CancellationToken` 传给共享上传入口；配置无效、请求取消、超时、网络异常或其他非 2xx 响应都会使当前 Step 失败并阻止后续步骤继续执行。上述准确的版本冲突仅记 Warning，Step 与 Batch 可继续；Batch 成功不表示被冲突拒绝的商品表已在服务端生效。

## 低层构造与调用边界

校验集中在正式 Protocol 创建链；内部 DTO 不重复承担所有不变量。Body 浅复制商品数组并隐藏可变集合接口，商品对象为原只读引用；Canonicalizer 假定输入已经按数值 TableId 排序，不自行排序、拒绝重复 ID 或校验商品类型。它固定键顺序、保留字符串码位、不做 Unicode 归一化；不成对的 UTF-16 代理项会抛异常，失败时已追加的缓冲不可当作有效结果。`ComputeHash` 只对给定字符串计算 SHA，不独立证明该字符串符合 JCS。

`CreateFromCurrentConfig` 的当前配置解析可能修复路径提示；它既不保存 Inspector 变更也不导出 Runtime，不能与 Action 的显式、严格只读 Prepare 混为一谈。精确桥只接受同一已加载桥创建的私有 Prepared 类型，Describe 不重新生成请求；任何 Submit 尝试都会消费它，busy、预取消或失败也不恢复。共享发送门只拒绝重入，不排队；取消可能发生在 HTTP 已派发之后，不能据取消推断远端未收到。兼容上传入口保留原响应日志和异常，Action 则只输出有限脱敏结果，二者日志边界不同。
