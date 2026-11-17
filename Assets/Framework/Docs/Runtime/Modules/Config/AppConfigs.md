# AppConfigs

`AppConfigs` 是 Runtime 应用配置，位于 `NovaFramework.Runtime`。

```csharp
[Serializable]
public sealed class AppConfigs
{
    public string AppID;
    public string AppAesKey;
    public string AppAesIV;
    public string CustomConfigCmdName;
    public string CustomName;
}
```

ConfigWindow 在 Editor 的三维矩阵中维护它，导出时按当前 `Platform × Channel × DevelopMode` 单值化到 `ConfigRuntimeSO.AppConfigs`。运行时通过 `IConfigManager.AppConfigs` 或 `Nova.Config.AppConfigs` 读取。

## 取值来源与格式

字段业务来源见 [AppConfigs 的字段提示](../../../../Scripts/Runtime/Modules/Config/Definitions/AppConfigs.cs)，协议约束见 [NetService：参数与失败语义](../Network/NetService.md)。

当前字段 Tooltip 还声明了 Nova Demo 展示专用的应用参数来源。Sample 资产中的占位值不代表整个公开包没有其他已声明来源；使用前仍须核对配套关系，本文不复制凭据，也不把存在或格式合法视为后台已验证。新项目启动不依赖取得这套 Demo 参数。

| 字段 | 真实来源与用途 | 填写前核对 |
| --- | --- | --- |
| `AppID` | 公司 GM 后台登记的项目应用 ID | 不是 Android 包名、Bundle ID 或游戏名；当前网络协议按 `int32` 解析 |
| `AppAesKey / AppAesIV` | GM 后台登记并与服务端对应的应用协议参数 | UTF-8 编码后各 16 字节；十六进制编码后的字符数不等于原始随机字节数；本地生成或长度合法均不能证明后台匹配 |
| `CustomConfigCmdName` | 项目已正式配置并导出的 NetCmd 名称 | 核对真实命令与路由，不从示例名推导 |
| `CustomName` | GM 后台 Custom 配置项的键，发送到 `PbNetAppCustomConfigReq.key` | 不是应用显示名称；使用后台实际配置项 |

从零到一尚无项目专有物料时，按 [NA-014：授权范围、实时合同和恢复回执约束实际执行](../../../Onboarding/DEVELOPMENT_CONSTRAINTS.md#na-014授权范围实时合同和恢复回执约束实际执行) 保留外部参数未配置/占位状态，不随机分配应用编号或协议密钥。真实可用且已获使用授权的 Demo 应用物料可以临时复用，但取得它们不是本地启动前提；使用时保持 AppID、Key/IV、Host/命令及配置项配套，记录临时来源和替换位置，不能改称产品正式登记。后续替换须重新导出并验证受影响消费者。

`AppAesKey / AppAesIV` 是应用配置字段，页面显示名分别为 `App Aes Key / App Aes IV`。它们必须原样保留，不用于 `Util.Encrypt.AES` 默认密钥或 Persist 本地数据，也不会迁移到 `PrivacyConfigs`。

- `CustomConfigCmdName`：启动 Custom 配置拉取使用的 NetCmd 名称；为空时关闭自动拉取。
- `CustomName`：发送到 `PbNetAppCustomConfigReq.key` 的 GM 配置项名称；为空时关闭自动拉取。

App 参数需按实际业务配置，公开包的 `YOUR_` 占位符不能用于业务请求。配置保存后需重新导出，并核对运行时实际使用的 `ConfigRuntimeSO`。Nova 不再通过 Project Guard 在进入 Play 前校验或拦截这些字段；参数约束与失败行为以当前调用方实现为准。

当前 [配置 Validator](../../../../Scripts/Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.Validator.cs) 与 Runtime 请求入口共用 [AppConfigValidation](../../../../Scripts/Runtime/Modules/Config/Managers/Definitions/AppConfigValidation.cs) 的本地格式判定：AppID 必须可解析为 int32，协议 Key/IV 必须为 UTF-8 各 16 字节，空白及 `YOUR_` 公开占位均视为未配置。设计态为各问题给出字段级 Warning，不阻断本地正式导出；本地 Privacy、序列化结构、资源等基础错误仍按各自契约处理。

启动自动拉取同时要求应用参数与配置项名称就绪；否则立即结束本轮远端等待，保留本地默认值，不等待 Network 来补外部物料。真正发起 Proto 请求时，无效 AES 参数仍返回 `AES_ENCRYPT_FAILED`，无效 AppID 现在返回客户端 `APP_CONFIG_INVALID`，不再把错误编号回退为 0 发出请求。合法参数与完整服务端匹配仍须独立验收。按 [NA-002：先核对当前能力，再选择接口或提出补充](../../../Onboarding/DEVELOPMENT_CONSTRAINTS.md#na-002先核对当前能力再选择接口或提出补充) 保留 Network 节点，分别验证[本地启动](../../../Onboarding/VALIDATION.md#缺外部物料的启动验收)与外部服务可用。

关联文档：[ConfigRuntimeSO.md](ConfigRuntimeSO.md)、[ConfigMasterSO.md](../../../Editor/Config/ConfigMasterSO.md)。
