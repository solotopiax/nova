# PrivacyConfigs

`PrivacyConfigs` 是 Runtime 隐私配置快照，只包含 `AESKey` 与 `AESIV`。两者按 UTF-8 编码后必须各为 16 字节，由 ConfigWindow 在 `Platform × Channel × DevelopMode` 三维矩阵中独立维护并导出。

它只用于 `Util.Encrypt.AES` 默认密钥初始化及 Persist 本地数据加解密，不属于 `AppConfigs.AppAesKey / AppAesIV`，也不会迁移或复用应用配置数据。

运行时必须先等待 `Nova.Config.LoadAsync()` 完成，再调用未显式传入 key/iv 的 AES 接口。Editor 下 Persist Inspector 通过 `WorkspaceActive` 定位 ConfigMaster，并按其当前合法坐标显式传入 Key/IV；不要求该 Platform 与 Unity `activeBuildTarget` 一致。

关联文档：[ConfigRuntimeSO.md](ConfigRuntimeSO.md)、[ConfigManager.md](ConfigManager.md)、[Util.Encrypt.md](../../Utils/Util.Encrypt.md)。
