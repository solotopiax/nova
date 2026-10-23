# PrivacyConfigs

`PrivacyConfigs` 是 Runtime 隐私配置快照，包含本地 AES 默认凭据、iOS 隐私声明和独立包通用扩展配置。全部字段由 ConfigWindow 在 `Platform × Channel × DevelopMode` 三维矩阵中维护，经同一维度投影、保存与导出链生成运行时快照。

| 字段 | 默认值 | 作用 |
| --- | --- | --- |
| `AESKey` / `AESIV` | 空 | 本地 AES 默认凭据；按 UTF-8 编码后必须各为 16 字节。 |
| `ExtensionConfigsJson` | `{}` | 按扩展键保存独立包的 JSON 配置，随本页三维配置深拷贝、投影、保存和导出；主框架不依赖具体包类型。 |
| `PrivacyInfoConfig` | iOS 参考模板 | iOS Required Reason API 清单，见下文。 |
| `InfoPlistUsageDescriptions` | `{}` | iOS 权限用途说明，见下文。 |

独立包通过通用 `PrivacyPanelExtensions` 注册隐私面板，在 `ExtensionConfigsJson` 内维护自己的配置。主框架只保留通用存储和兼容迁移边界；未安装扩展包时不显示其面板，已保存的扩展 JSON 仍保留。运行时扩展可通过 `IPrivacyConfigProvider.PrivacyConfigs` 读取已加载的快照，无需修改 `IConfigManager` 的既有实现契约。

内存防护属于可选包 `com.solotopia.nova.framework.memory.protection`。它在同名扩展键下保存 `EnableMemoryTamperDetection`、`EnableMemoryKeyRotation`，默认均为 `true`；旧资产已有的两个开关（包括 `false`）会迁入扩展配置。字段含义、商业插件安装、业务接入与生命周期见 [MemoryProtection 包文档](../../../../../../UPMPackages/com.solotopia.nova.framework.memory.protection/Nova/Docs/MemoryProtection.md)。

自然语言配置使用 `config.patch-panels` 时，初始 Key/IV 可以逐步填写，原有有效 Key/IV 同值请求和其它隐私字段编辑保持可用；已有有效 Key/IV 更换、清空或被维度广播覆盖时阻断。需要保留旧档的轮换由同一 `nova-project-integrate-persistence` 的独立 Privacy 分支处理：当前 macOS Editor 标准三存储、激活 Master 当前 Privacy 逻辑组、配对 Runtime、独立封存恢复与明确消费者范围，详见[受控存档轮换](../../../Editor/EditorUtil/EditorUtil.AgentActions/EditorUtil.AgentActions.PersistenceMaintenance.md)。ConfigWindow 手动保存保持原行为；只改配置并重新导出不能证明旧存档及恢复材料仍可读取，不能用普通字段编辑替代迁移。配置面板边界见 [Config 面板编辑](../../../Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.PanelEditing.md)。

`AESKey / AESIV` 只用于 `Util.Encrypt.AES` 默认密钥初始化及 Persist 本地数据加解密，不属于 `AppConfigs.AppAesKey / AppAesIV`，也不会迁移或复用应用配置数据。

`PrivacyInfoConfig` 用于 iOS 应用级隐私清单的 Required Reason API 声明，与 AES 无关。新建配置默认填入 Solar 的参考值，包含 FileTimestamp、SystemBootTime、DiskSpace、ActiveKeyboards（空数组）和 UserDefaults；旧 `ConfigMasterSO` 首次在 ConfigWindow 打开时也会在工作副本中一次性补齐空值，待用户保存后落盘。已有非空自定义值不被覆盖，之后手动清空也不会再次补回。ConfigWindow 仅在编辑 iOS 平台时允许修改；其他平台灰显，不参与构建。填写格式为“API 类别 → 理由码数组”的原始 JSON 对象，例如 `{"NSPrivacyAccessedAPICategoryFileTimestamp":["C617.1"]}`；输入框内的双引号不需要加反斜杠，只有写成 C# 字符串字面量时才需要按语言语法处理引号。手动清空字段或仅保留空数组时不注入应用级声明。默认模板不是合规保证，项目上线前必须依据实际使用的 API 核对类别及理由码。ConfigWindow 在 iOS 配置导出前校验 JSON 结构，构建时读取当前已导出的 `ConfigRuntimeSO`，将非空类别合并到 Xcode 输出根目录的 `PrivacyInfo.xcprivacy` 并注册到主应用 target；已有清单的其他字段及同类别理由码保留。若项目还包含第三方 SDK 自带的清单，需分别审查其声明，不能以本字段替代。

`InfoPlistUsageDescriptions` 是另一份仅 iOS 生效的 JSON 对象，填写“`UsageDescription` 键 → 面向用户的真实用途说明”，例如 `{"NSPhotoLibraryUsageDescription":"用于从相册选择照片"}`。配置默认为 `{}`；旧资产缺少字段、手动留空或 `{}` 都不注入条目。非空配置在 ConfigWindow 导出前校验键名和文案，iOS 构建时从当前 `ConfigRuntimeSO` 写入应用 `Info.plist`，同名键以当前配置为准，其他原有键保留。框架不预置应用文案，也不自动推断项目或第三方 SDK 所需的全部键；项目必须按实际功能填写并检查最终归档中的应用 `Info.plist`。此字段与 `PrivacyInfo.xcprivacy` 互不替代。

运行时必须先等待 `Nova.Config.LoadAsync()` 完成，再调用未显式传入 key/iv 的 AES 接口。若尚未注入或字段无效，AES Error 会明确指向 `Nova/Open Config → 通用配置 → 隐私配置`，要求为当前 `Platform × Channel × DevelopMode` 配置 `AES-Key / AES-IV`（UTF-8 各 16 字节）并重新导出 `ConfigRuntimeSO`。

Editor 内尚未结束的 Privacy 维护会通过固定标记阻断 Config/Persist 加载、Play与构建；检查在已加载的幂等短路之前进行，直到正规数据/配置后验与门禁清场完成。此门禁只在 `UNITY_EDITOR` 编译，不能解释为已经迁移设备、服务端或全部业务默认 AES 消费者。

启用任一 Persist AES 开关时，`Nova.Persist.LoadAsync()` 会在存储实现初始化前检查默认凭据；缺配直接抛出，不能把“存储实现初始化返回”误当成已经解密了 PlayerPrefs 或 SQLite 存档。因此标准顺序固定为：

```csharp
await Nova.Config.LoadAsync();
await Nova.Persist.LoadAsync();
```

Editor 下 Persist Inspector 通过 `WorkspaceActive` 定位 ConfigMaster，并按其当前合法坐标显式传入 Key/IV；其中 Platform 由 `CurrentPlatform` 实时映射 Unity `activeBuildTarget`，要查看或编辑另一平台份必须先切换 Unity BuildTarget。

关联文档：[ConfigRuntimeSO.md](ConfigRuntimeSO.md)、[ConfigManager.md](ConfigManager.md)、[Util.Encrypt.md](../../Utils/Util.Encrypt.md)。
