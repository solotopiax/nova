# PrivacyConfigs

`PrivacyConfigs` 是 Runtime 隐私配置快照，包含 `AESKey`、`AESIV` 和 `PrivacyInfoConfig`。前两者按 UTF-8 编码后必须各为 16 字节，由 ConfigWindow 在 `Platform × Channel × DevelopMode` 三维矩阵中独立维护并导出。

`AESKey / AESIV` 只用于 `Util.Encrypt.AES` 默认密钥初始化及 Persist 本地数据加解密，不属于 `AppConfigs.AppAesKey / AppAesIV`，也不会迁移或复用应用配置数据。

`PrivacyInfoConfig` 用于 iOS 应用级隐私清单的 Required Reason API 声明，与 AES 无关。新建配置默认填入 Solar 的参考值，包含 FileTimestamp、SystemBootTime、DiskSpace、ActiveKeyboards（空数组）和 UserDefaults；旧 `ConfigMasterSO` 首次在 ConfigWindow 打开时也会在工作副本中一次性补齐空值，待用户保存后落盘。已有非空自定义值不被覆盖，之后手动清空也不会再次补回。ConfigWindow 仅在编辑 iOS 平台时允许修改；其他平台灰显，不参与构建。填写格式为“API 类别 → 理由码数组”的原始 JSON 对象，例如 `{"NSPrivacyAccessedAPICategoryFileTimestamp":["C617.1"]}`；输入框内的双引号不需要加反斜杠，只有写成 C# 字符串字面量时才需要按语言语法处理引号。手动清空字段或仅保留空数组时不注入应用级声明。默认模板不是合规保证，项目上线前必须依据实际使用的 API 核对类别及理由码。ConfigWindow 在 iOS 配置导出前校验 JSON 结构，构建时读取当前已导出的 `ConfigRuntimeSO`，将非空类别合并到 Xcode 输出根目录的 `PrivacyInfo.xcprivacy` 并注册到主应用 target；已有清单的其他字段及同类别理由码保留。若项目还包含第三方 SDK 自带的清单，需分别审查其声明，不能以本字段替代。

运行时必须先等待 `Nova.Config.LoadAsync()` 完成，再调用未显式传入 key/iv 的 AES 接口。若尚未注入或字段无效，AES Error 会明确指向 `Nova/Open Config → 通用配置 → 隐私配置`，要求为当前 `Platform × Channel × DevelopMode` 配置 `AES-Key / AES-IV`（UTF-8 各 16 字节）并重新导出 `ConfigRuntimeSO`。

启用任一 Persist AES 开关时，`Nova.Persist.LoadAsync()` 会在存储实现初始化前检查默认凭据；缺配直接抛出，不能把“存储实现初始化返回”误当成已经解密了 PlayerPrefs 或 SQLite 存档。因此标准顺序固定为：

```csharp
await Nova.Config.LoadAsync();
await Nova.Persist.LoadAsync();
```

Editor 下 Persist Inspector 通过 `WorkspaceActive` 定位 ConfigMaster，并按其当前合法坐标显式传入 Key/IV；其中 Platform 由 `CurrentPlatform` 实时映射 Unity `activeBuildTarget`，要查看或编辑另一平台份必须先切换 Unity BuildTarget。

关联文档：[ConfigRuntimeSO.md](ConfigRuntimeSO.md)、[ConfigManager.md](ConfigManager.md)、[Util.Encrypt.md](../../Utils/Util.Encrypt.md)。
