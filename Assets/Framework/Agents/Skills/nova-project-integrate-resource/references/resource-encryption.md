# 资源加密、密钥源码与轮换

用户要求已确认 Nova 业务资源加密、资源密钥配置或轮换时读取本页及随包 `Docs/Runtime/Modules/Asset/ResourceEncryption.md`。不迁移插件/非 Nova 资源，不生成框架通用秘密，不把 Base64 当防逆向。

## 冻结与选择

从当前持久化 Master/坐标、Package、Collector、正式版本、资源类型和客户端代码归属读取事实，冻结包名、Bundle/Manifest 方案、有效 keyId、密钥源码目录及旧首包/缓存/回退集合。需要真实密钥时通过项目受保护配置渠道输入，日志只保留包名/keyId/hasValue，不回显 KeyBase64、密钥字节或生成源码正文。项目密钥配置与生成代码保留在项目私有仓库，不能写入 Framework 包、副仓公开物料或 `ConfigRuntimeSO`。

| 构建选择 | 真实类型 | 读取与边界 |
|---|---|---|
| Bundle Offset | `NovaFramework.Editor.ResourceOffsetEncryptor` | 无密钥轻度混淆，默认去头流 |
| Bundle XOR | `NovaFramework.Editor.ResourceXorEncryptor` | 弱混淆，整包内存解密 |
| Bundle AES memory | `NovaFramework.Editor.ResourceAesMemoryEncryptor` | AES-256 CBC + HMAC-SHA256，整包认证/内存解密 |
| Bundle AES stream | `NovaFramework.Editor.ResourceAesStreamEncryptor` | 64 KiB 分块认证；原生 Unity AssetBundle 可流式读取 |
| Manifest AES | `ResourceManifestAesEncryptor` + `ResourceManifestAesDecryptor`，均在 `NovaFramework.Editor` | 必须配套，Runtime 由 Nova 注入 |
| Manifest XOR | `ResourceManifestXorEncryptor` + `ResourceManifestXorDecryptor`，均在 `NovaFramework.Editor` | 必须配套，不能当密码学认证 |

所有有密钥方案要求配置中每个 KeyBase64 解码为 32 字节；包名/keyId 按 `ResourceKeyStore.Validate` 的真实约束检查。keyId 是密钥编号，不是版本号或秘密。WebGL 的加密 Unity AssetBundle 和所有平台的 RawBundle/ArchiveBundle 都走内存解密；AES stream 不给这些类型带来流式内存收益。Android 内置加密 Bundle 先解包到文件系统，验收还要确认首包时间与磁盘。

## 当前入口与准确参数

| 入口 | 精确输入/来源 | 实际写入与能力边界 |
|---|---|---|
| `nova.project.config.inspect-panel` | `masterGuid` 与真实坐标的 `target.panel="ResourceEncryption"`，不传 `configType` | 只读真实相对字段路径、editable、脱敏值、独立编号维度；不保存、不回显秘密，先按 live Schema 发现 |
| `nova.project.config.patch-panels` | 同一 Master 与 ResourceEncryption `changes`；fields 的 path 来自 Inspect，valueJson 为严格 JSON；维度通过 dimensions | 独立工作副本预演后仅保存私有 Master；支持新增 Package/Key、独立编号维度与切换有效编号，保留全部旧包/旧编号/原字节；不生成密钥源码或 Runtime |
| `nova.project.config.generate-resource-keys` | `masterGuid`、`platform`、`channel`、`developMode`，均为精确枚举名；输出目录来自已保存 Master，不传密钥或路径参数 | 对任意指定已保存 Master 只读计划、单次生成源码/link 并仅保存生成路径状态；冻结配置/meta、真实 Player 编译图、精确替换/删除/新目录，等待本次实际编译重载后 Verify；不激活 Master、不导出 Runtime |
| ConfigWindow 保存并顶部导出 | 当前已保存 Master、明确 Platform/Channel/DevelopMode；资源密钥 Draft 与真实保存契约 | 导出密钥源码、link.xml 和 Runtime/YooAsset 配置；不是任意窗口字段 MCP Action |
| Pipify `export.config` | `ConfigExportParams.Platform` 取 Active BuildTarget，`Channel` / `DevelopMode` 是真实 Step 参数 | public `EditorUtil.Config.Exporter.Export(master, platform, channel, mode, savePath)` 同轮显式生成密钥源码；先配置/审查 Batch 后经 run-batch |
| `ResourceKeyCodeGenerator.Generate(ConfigMasterSO master)` | 真实 Master 当前坐标 | C# 底层入口，生成代码、link.xml 和 GeneratedCodeAssetPath 状态；不是独立 MCP Tool |
| `ResourceKeyCodeGenerator.Generate(ConfigMasterSO master, DimensionProjector.Coord coord)` | 明确 coord 的 Platform/Channel/Mode，Namespace 由该坐标解析 | 同上；不改 Master 当前渠道/模式，不创建 asmdef |
| `ResourceKeyCodeGenerator.GenerateForCI()` | **无命令参数**，从 `WorkspaceActive.Get()` 取激活已保存 Master | 生成后 SetDirty(master) + SaveAssets；CI 必须使用实际已有 Unity 宿主入口并避免其它脏资产被保存，不传虚构 --master/--key |
| `ResourceKeyCodeGenerator.ValidateGenerated()` | **无参数**，激活已保存 Master + 当前编译程序集 | Player 前检查配置、唯一源码、link.xml 与实际程序集一致；不在构建中补生成/编译 |
| `nova.project.config.export-runtime` | live Schema 中的运行配置请求 | 当前受控 Exporter 调用 `exportResourceKeys=false`，**不生成资源密钥源码**，不能替代上面生成链 |

旧 `Generate(ResourceKeyStoreData)` 已 Obsolete，且只接受激活 Master 同一引用；不要另建 JSON/环境变量来源。底层 `GenerateForCI` 方法可用不代表 shell/MCP 自带宿主；消费端独立生成用上表强类型 Action，不插任意 C# / reflection / 临时 Unity runner。

资源密钥编辑复用已暴露的 Config 面板 Action，详细请求与边界见 [Config 面板参考](../../nova-project-configure-runtime/references/panel-editing.md)。先 `describe` → `inspect-panel`，再以实际可编辑路径 `plan/execute/verify`：不照抄数组路径、不直接编辑 DimensionMask，不允许删除旧 Package/Key 或替换既有 keyId 的字节。开启/关闭编号维度经既有分裂/合并，关闭轴时来源取 target 的该轴坐标；ResourceEncryption 顶层字典不按矩阵字段广播。Master 脏态或窗口草稿、非规范结构、请求外归一变化、文件/完整状态漂移均阻塞当前计划。

密钥只通过已授权私有配置输入传入面板 Action；任务说明、终端日志和公开证据不输出 valueJson、KeyBase64、生成源码或逐密钥摘要。Inspect、Plan 与 Receipt 仅保留脱敏状态、完整目标指纹和物理变化路径；完整 Master 摘要不是单个密钥摘要。保存完成还要 Verify 并重新 Inspect active keyId，再调用 `generate-resource-keys` 的 describe → plan → execute → verify。该生成请求不携带密钥正文，不读窗口草稿；平台须与 Unity 当前 Active BuildTarget 一致，None 渠道、未保存状态、草稿、Framework/Packages 输出、手写冲突、另一个生成 provider 或漂移均阻断。通用 `settings.patch-target` 仍不能编辑 ConfigMaster，固定 `config.patch-coordinate` 仍只有其字符串字段；不能手写 YAML 或绕过当前领域计划。仅保存配置或生成源码不能宣布全套加密接入成功。

生成计划明确列出 Master、源码/link、必要 meta、新目录及旧工具标记文件删除。若精确旧 `Assets/NovaGenerated/ResourceKeys/NovaProject.ResourceKeys.asmdef` 将被生成器删除并改变程序集归属，独立 Action 无法在只读计划中确认删除后的真实归属，因此阻断该旧格式分支；先经现有 ConfigWindow 顶部导出完成一次迁移，再重计划。Execute 开始后的失败为 partial，禁止重放；Plan 失效或脚本重载后只能用已绑定 Receipt Verify。Verify 成功要求本次真实编译、无错误、重载以及完整保存状态、文件摘要、删除结果、唯一 provider 和 Player assembly；它不证明 Bundle、Player 安装或真机读取。

## 权威链与成功标准

1. 保存项目私有 `ConfigMasterSO.ResourceEncryption`；`ClientCodeOutputDirectory` 是 Assets 内客户端目录，不能含 Editor。正式生成名为 `ResourceKeys.Generated.cs`，旁边是工具专属 `link.xml`；不覆盖手写同名源码/linker。
2. 经 `nova.project.config.generate-resource-keys` 或已有生成链导出并等待实际导入/编译。用 `CompilationPipeline` 确认源码的实际 Player assembly 直接引用 `NovaFramework.Runtime`，且自身及依赖闭包不属于 HybridCLR 热更程序集；不从目录/namespace猜 DLL。提供器在 SubsystemRegistration 注册，Preserve/linker 确保启动前可用。
3. 核对唯一生成提供器、源码与配置/Namespace、当前坐标 active keyId、link.xml 的真实 assembly/type 一致。项目无 key 且从未生成源码时是空配置合法状态，不冒称加密已启用。
4. 用已选定配套 Encryptor/Decryptor 构建本次 Bundle。正式版本目录的 Bundle 名可仍为 `.bundle`，内容已加密；`.encrypt` 只是 OutputCache 中间文件。上传/首包只取正式版本。`ClearBuildCacheFiles` 默认 false；开启会在下次构建清整个平台/包输出根及历史版本，必须先保留已交付版本。
5. 当前 Player/IL2CPP/浏览器读取真实 Manifest、首包或 CDN 资源，验证相应加载/释放、认证失败路径和本次 keyId，按任务实测内存/性能。Editor codec 测试或 BuildResult 不证明真机。需要部署路由到既有 pipeline [部署参考](../../nova-project-configure-pipeline/references/platform-deployment.md)，远端权限单独冻结。

## 轮换和恢复

旧首包/缓存/回退是 v1、新资源是 v2 时，客户端同时保留同 Package 的 v1/v2；当前编号只控制后续构建。禁止在同 keyId 下替换密钥。移除 v1 前需确认所有保留首包、缓存、旧版本回退均不再引用 v1；只重编客户端不会重新加密资源。多个 Master 每次仅生成当前集合，不自动合并，一工程只允许一份实际生成提供器。

保存/源码路径变化/正式构建分别验收；源码迁移由生成器按其标记处理旧产物，失败报告实际边界，不手删未知文件。清理本轮测试对象、临时 runner、密码文件、样本密文/日志与临时配置，保留项目正式密钥源码与已授权 Bundle/Player，恢复用户旧状态。报告配置、生成/编译、构建、运行、远端各层及清场；缺必需入口、产物或真实运行时为 `partial` / `blocked`。
