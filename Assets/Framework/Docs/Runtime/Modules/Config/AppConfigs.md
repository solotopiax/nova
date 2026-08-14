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

Editor 进入 Play Mode 前，`ProjectGuard` 会检查当前 Demo 实际导出的 App 参数：`AppID` 必须是正整数，`AppAesKey` / `AppAesIV` 必须是 16 字节 UTF-8 字符串，且不能保留公开包的 `YOUR_` 占位符。异常时会阻止启动；弹窗仅显示字段异常与 ConfigWindow 入口，字段入口严格使用页面显示名 `App Aes Key / App Aes IV`，完整的 ConfigMaster 来源、ConfigRuntime 导出物和当前 `Platform × Channel × DevelopMode` 坐标见 Console / Editor.log。配置保存后需要重新导出。

关联文档：[ConfigRuntimeSO.md](ConfigRuntimeSO.md)、[ConfigMasterSO.md](../../../Editor/Config/ConfigMasterSO.md)。
