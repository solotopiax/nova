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

| 字段 | 真实来源与用途 | 填写前核对 |
| --- | --- | --- |
| `AppID` | 公司 GM 后台登记的项目应用 ID | 不是 Android 包名、Bundle ID 或游戏名；当前网络协议按 `int32` 解析 |
| `AppAesKey / AppAesIV` | GM 后台登记并与服务端对应的应用协议参数 | UTF-8 编码后各 16 字节；十六进制编码后的字符数不等于原始随机字节数；本地生成或长度合法均不能证明后台匹配 |
| `CustomConfigCmdName` | 项目已正式配置并导出的 NetCmd 名称 | 核对真实命令与路由，不从示例名推导 |
| `CustomName` | GM 后台 Custom 配置项的键，发送到 `PbNetAppCustomConfigReq.key` | 不是应用显示名称；使用后台实际配置项 |

从零到一尚无项目专有物料时，可按 [NA-014：授权范围、实时合同和恢复回执约束实际执行](../../../Onboarding/DEVELOPMENT_CONSTRAINTS.md#na-014授权范围实时合同和恢复回执约束实际执行) 临时复用 Nova 任一 Demo 的应用物料；保持该 Demo 的 AppID、Key/IV 与所用 Host/命令/配置项匹配，记录出处、临时用途及替换位置。它们仍是 Demo 对应的应用参数，不能改称本产品正式登记；后续替换后须重新导出并验证受影响消费者。

`AppAesKey / AppAesIV` 是应用配置字段，页面显示名分别为 `App Aes Key / App Aes IV`。它们必须原样保留，不用于 `Util.Encrypt.AES` 默认密钥或 Persist 本地数据，也不会迁移到 `PrivacyConfigs`。

- `CustomConfigCmdName`：启动 Custom 配置拉取使用的 NetCmd 名称；为空时关闭自动拉取。
- `CustomName`：发送到 `PbNetAppCustomConfigReq.key` 的 GM 配置项名称；为空时关闭自动拉取。

App 参数需按实际业务配置，公开包的 `YOUR_` 占位符不能用于业务请求。配置保存后需重新导出，并核对运行时实际使用的 `ConfigRuntimeSO`。Nova 不再通过 Project Guard 在进入 Play 前校验或拦截这些字段；参数约束与失败行为以当前调用方实现为准。

`CustomConfigCmdName / CustomName` 留空关闭自动拉取，只描述 Runtime 的开关语义；当前 [配置 Validator](../../../../Scripts/Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.Validator.cs) 仍要求 `AppID / AppAesKey / AppAesIV` 非空，[受控导出](../../../../Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Generate/ConfigExportRuntimeAction.cs) 使用该校验。项目专有与 Demo 临时来源均不能满足时，应报告这项前置或入口缺口，不能声称留空即可导出，也不能伪造参数或改走任意 C# 导出。保留标准 Network 预加载节点不代表已接入 GM 服务，保存、导出与真实请求验收分别报告。

关联文档：[ConfigRuntimeSO.md](ConfigRuntimeSO.md)、[ConfigMasterSO.md](../../../Editor/Config/ConfigMasterSO.md)。
