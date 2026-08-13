# PrivacyConfigs

`PrivacyConfigs` 是 Runtime 隐私配置快照，只包含 `AESKey` 与 `AESIV`。两者按 UTF-8 编码后必须各为 16 字节，由 ConfigWindow 在 `Platform × Channel × DevelopMode` 三维矩阵中独立维护并导出。

它只用于 `Util.Encrypt.AES` 默认密钥初始化及 Persist 本地数据加解密，不属于 `AppConfigs.AppAesKey / AppAesIV`，也不会迁移或复用应用配置数据。

运行时必须先等待 `Nova.Config.LoadAsync()` 完成，再调用未显式传入 key/iv 的 AES 接口。若尚未注入或字段无效，AES Error 会明确指向 `Nova/Open Config → 通用配置 → 隐私配置`，要求为当前 `Platform × Channel × DevelopMode` 配置 `AES-Key / AES-IV`（UTF-8 各 16 字节）并重新导出 `ConfigRuntimeSO`。

启用任一 Persist AES 开关时，`Nova.Persist.LoadAsync()` 会在存储实现初始化前检查默认凭据；缺配直接抛出，不能把“存储实现初始化返回”误当成已经解密了 PlayerPrefs 或 SQLite 存档。因此标准顺序固定为：

```csharp
await Nova.Config.LoadAsync();
await Nova.Persist.LoadAsync();
```

Editor 下 Persist Inspector 通过 `WorkspaceActive` 定位 ConfigMaster，并按其当前合法坐标显式传入 Key/IV；不要求该 Platform 与 Unity `activeBuildTarget` 一致。

关联文档：[ConfigRuntimeSO.md](ConfigRuntimeSO.md)、[ConfigManager.md](ConfigManager.md)、[Util.Encrypt.md](../../Utils/Util.Encrypt.md)。
