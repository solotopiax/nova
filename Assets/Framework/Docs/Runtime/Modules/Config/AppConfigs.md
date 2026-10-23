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

`AppAesKey / AppAesIV` 是应用配置字段，页面显示名分别为 `App Aes Key / App Aes IV`。它们必须原样保留，不用于 `Util.Encrypt.AES` 默认密钥或 Persist 本地数据，也不会迁移到 `PrivacyConfigs`。

- `CustomConfigCmdName`：启动 Custom 配置拉取使用的 NetCmd 名称；为空时关闭自动拉取。
- `CustomName`：发送到 `PbNetAppCustomConfigReq.key` 的 GM 配置项名称；为空时关闭自动拉取。

App 参数需按实际业务配置，公开包的 `YOUR_` 占位符不能用于业务请求。配置保存后需重新导出，并核对运行时实际使用的 `ConfigRuntimeSO`。Nova 不再通过 Project Guard 在进入 Play 前校验或拦截这些字段；参数约束与失败行为以当前调用方实现为准。

关联文档：[ConfigRuntimeSO.md](ConfigRuntimeSO.md)、[ConfigMasterSO.md](../../../Editor/Config/ConfigMasterSO.md)。
