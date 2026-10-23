# 提交已导出的 IAP 商品目录

仅在目标是提交完整商品目录或核对一次提交结果时读取本页，再读实际已解析 IAP 包的 `Nova/Doc/INDEX.md → IAPProductCatalogProtocol.md`。此分支复用 `nova.project.iap.upload-product-catalog`，不新建 Skill，不替代商品表维护、配置导出或支付业务验收。

## 冻结与授权

- 用当前 Provider 的 `describe` 确认 Action 和严格 Request Schema。请求仅有 `runtimeGuid`（现存 `ConfigRuntimeSO` 的 N 格式 GUID）与 `developMode`（精确 `Debug` / `Release`），不传地址、Header、Key/IV、商品数组、Body、代码或重试参数。
- 只接受持久化激活 Master 的已保存显式 `ExportTarget`；Master/Runtime 不得 dirty，导出坐标及模式必须一致。Action 不导出、不保存配置，也不修复 `Globals.json` 的 pathHint。缺包返回 `not_applicable`；目标、草稿、桥身份或前置不满足为 `blocked`，先沿原配置/UPM能力处理确需的缺项，不猜相邻坐标。
- Plan 冻结 Master、其 meta、Runtime、其 meta、`ProjectSettings/Nova/Globals.json`、`ProjectSettings/ProjectSettings.asset` 六文件与两对象内存摘要、活动目标、应用 ID/坐标、`PlayerSettings.bundleVersion`、固定上传地址、完整规范目录及最终请求身份、固定 IAP 桥及代码身份。密文请求仅驻原计划内存；对外只显示环境、版本、目录 Hash/数量和请求 Hash/字节数。
- Debug 和 Release 分别对应包协议中的测试/正式后台。真实上传必须已获针对该环境、接收目标、目录摘要、客户端版本及至多一次请求的精确授权；本地配置、Batch保存、静态审查或假发送器测试均不包含真实上传授权。沿用已有精确授权，无需重复询问；目标或目录变化则重新冻结。
- Key/IV 仅来自同一 Runtime 的 `AppConfigs.AppAesKey/AppAesIV`，不回退到 Privacy 默认 AES；确认和报告只给字段名/存在性，不输出凭据、请求 Body 或原始响应。

## 一次提交与只读核验

```json
{
  "operation": "plan",
  "action_id": "nova.project.iap.upload-product-catalog",
  "request": {
    "runtimeGuid": "<已发现的现存Runtime GUID>",
    "developMode": "Debug"
  }
}
```

只有 Plan `ready`、精确授权已满足时，才使用原 `plan_id` 与绑定确认令牌执行一次。Execute 重读冻结输入并比较身份，在网络前于 `Library/Nova/AgentActions/IapCatalogUploads/<attemptId>.json` 独占写入并读回正式发送意图；保存失败不发送。随后只提交原 Prepared 的最终请求，不按当前配置生成另一份请求，不排队重入、不重试。Inspector、Pipify 与 Action 共用发送门。

以原结果的 `recovery_token` 调用同 Action 的 `verify`。Verify 只读取原 attempt 的脱敏记录与指纹，不调用 Prepare/Submit，不向后台查询，也不重发。保留正式 Operation、发送意图和结果作为证据；它们不是临时测试日志。重载后原计划失效，只能核验已有结果；只有意图、缺失/损坏记录或发送后结果未落盘时保持 `partial`，不能据此推断远端没有写入。

| 实际结果 | 应报告的边界 |
|---|---|
| 2xx / `http-accepted` | HTTP 接受已观察；当前没有权威目录应用响应或查询契约，最高 `partial`，不称后台落库成功 |
| 精确 HTTP 409 且 `code=CATALOG_VERSION_CONFLICT` | `remoteApplication=not-applied`，本次新目录未应用；保持 `partial`，不自动改版本、覆盖或重发 |
| 非精确 409、其他 HTTP 错误、传输错误、取消、busy | 保留实际状态码/发送事实与有限异常分类，保持 `partial`；发送后未知不等未应用 |
| 只有发送意图、响应或记录不完整 | 实际远端状态未知；读取原记录，不能恢复 Execute 或自动再提交 |

公开回执只包含目录/请求身份、attempt 身份、发送事实、状态码、响应长度/SHA、有限结果分类和时间，不含 Key/IV、请求明文/密文 Body、原始响应或异常正文。`Static + Artifact` 只证明本地冻结和回执完整；不补造缺失的 External 应用证据。

## 既有入口与业务边界

旧 Inspector 按钮和无参数 Pipify Step `iap.product_catalog.upload` 仍通过 `IAPProductCatalogUploader.UploadCurrentAsync(CancellationToken)` 在执行时读取当前导出配置；其精确版本冲突保持 Warning 后正常返回。通用 Batch 未因此获得独立目录 Plan 的预冻结，Step/Batch成功也不证明目录应用。不要用任意 C#、反射、改 Step 或重跑 Batch 代替本 Action。

商品目录提交不创建支付订单、不验单、不补单、不发货；`sdk-kit.iap.business` 的交易、设备、服务端和幂等发货证据仍独立。本分支的无网络测试只验证冻结、发送次数、脱敏及恢复边界，不登记真实上传或后台验收。
