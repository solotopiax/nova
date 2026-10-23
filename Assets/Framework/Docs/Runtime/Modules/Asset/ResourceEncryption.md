# 资源加解密

资源加解密由 Nova 的 Asset 模块适配 YooAsset；密码学实现不进入 YooAsset Core。本地 YooAsset Editor 提供可实例化的构建服务候选，Bundle Builder 与 Pipify 共用。Config 管理密钥、当前编号与源码导出目录，构建方案仍在两个页面既有的三个加解密字段中选择。

## 项目组操作

1. 打开 `Nova/Open Config → 通用配置 → 资源加解密配置`。
2. 从“选择资源包”选择 YooAsset Collector 已配置的 PackageName，点击密钥列表右上角“新增密钥”，在黄色条目中选择自动生成或手动输入，再添加 32 字节 Base64 密钥，填写资源包内唯一的 `keyId`（例如 `v1`），设为当前加密密钥。这里的资源包就是 YooAsset Package，不是单个 Bundle 文件。
3. 设置客户端密钥源码导出目录（Assets 相对路径），点击顶部“保存”写入 ConfigMaster；需要更新客户端代码时再点击“导出”，等待 Unity 编译完成。保存不生成源码。生成源码及 link.xml 放在所选目录，命名空间使用 Config 配置，程序集由目录归属决定，不生成固定 asmdef。ConfigMaster 与生成文件、meta 共同纳入项目私有仓库；密钥不导出到 ConfigRuntimeSO。旧独立 JSON 自动迁入工作副本，保存并核对后移除。
4. 在 YooAsset Bundle Builder / Pipify 选择 Bundle Encryptor。清单加密独立选择：AES 配套 `NovaFramework.Editor.ResourceManifestAesDecryptor`，XOR 配套 `NovaFramework.Editor.ResourceManifestXorDecryptor`；客户端由 Nova 自动接入解密器，无需另设。
5. 构建 Bundle，再构建 Player。Bundle 构建校验密钥与清单加解密配对；Player 构建额外检查已生成源码、裁剪声明与保存配置是否一致，陈旧源码会阻断 Player 构建。

已有资源仍需要原来的 keyId 和密钥；新版客户端保留所有仍需解密的密钥。新密钥资源上线前，消费它的客户端必须已携带该密钥。具体例子见下文“密钥轮换”。

这里不创建或删除 YooAsset Package。删除操作只移除密钥配置；已不在 Collector 中的旧配置保留并提示，以便维护旧资源密钥。

页面标题提供“平台类型 / 渠道类型 / 开发模式”维度选择：未勾选的轴共用当前加密编号，勾选的轴分别选择，通过 Config 顶部切换编辑。各包的密钥字典始终共用，编号在包内唯一；不同平台需要不同密钥时新增不同编号，再各自设为当前。减维只合并当前编号选择，不删除旧密钥。独立 `DimensionMask` 与各包 `ActiveKeyOverrides` 保存在 ConfigMaster.ResourceEncryption；旧配置缺少这些字段时默认全局共用。保存自动清理失效的编号分支，完整保留所有密钥，详见 [ConfigMasterSO](../../../Editor/Config/ConfigMasterSO.md)。

Nova / Pipify 构建冻结实际目标平台，以及激活 Config 已保存的渠道和开发模式；`export.config` Step 的独立参数不会自动改写此坐标，请先保存对应 Config 坐标。官方 Bundle 加密从默认 OutputCache 路径取实际平台；官方清单加密只传字节数据，无法取得平台，按平台分维并启用 Nova 清单加密时须使用 Nova / Pipify 构建入口，否则明确阻断。生成客户端源码始终包含全部保留密钥，解密仍按资源文件中的 PackageName/keyId 精确查找，不依赖 ConfigRuntime 或当前环境。

## 方案选择

同一 Package 的一次构建使用一个 Bundle Encryptor，各 Package 或不同构建可以选择不同方案。当前没有按 Bundle 大小自动选择方案，也没有逐 Bundle 配置界面；客户端从文件头识别实际格式，不代表构建时会自动按大小分配。

| Bundle Encryptor | 定位 | 原生平台加载 |
|---|---|---|
| YooAsset.Editor.EncryptionNone | 不加密 | YooAsset 原路径 |
| NovaFramework.Editor.ResourceOffsetEncryptor | 追加头部的轻度混淆，无密钥 | 可定位的去头流 |
| NovaFramework.Editor.ResourceXorEncryptor | 循环 XOR，弱混淆 | 整包内存解密 |
| NovaFramework.Editor.ResourceAesMemoryEncryptor | AES-256 CBC + HMAC-SHA256 | 整包认证与内存解密 |
| NovaFramework.Editor.ResourceAesStreamEncryptor | AES-256 CBC + HMAC-SHA256 分块认证 | 64 KiB 分块随机读取 |

内存与流式是读取方式，不表示不同密码学强度。AES 使用随机文件标识、随机 IV，以及分离的加密/认证派生密钥；头部认证，块认证绑定文件头、位置、长度、IV 和密文。未认证块不得交给 Unity。

Android/iOS 项目可优先采用 AES stream，减少大 AssetBundle 解密时的整包内存占用；小包可选 AES memory，实际阈值由项目测量决定。Offset/XOR 适合轻度混淆，不能替代 AES 的保密与完整性认证。框架提供备选实现，项目仍需选择并验证自己的方案。

两种 Nova 加密清单方案位于 `NovaFramework.Editor` 命名空间，一一对应；不加密使用官方 None：

| Manifest Encryptor | Manifest Decryptor |
|---|---|
| YooAsset.Editor.ManifestEncryptorNone | YooAsset.Editor.ManifestDecryptorNone |
| NovaFramework.Editor.ResourceManifestAesEncryptor | NovaFramework.Editor.ResourceManifestAesDecryptor |
| NovaFramework.Editor.ResourceManifestXorEncryptor | NovaFramework.Editor.ResourceManifestXorDecryptor |

两种构建解密器分别校验 AES memory、XOR 格式，再从 ConfigMaster 提供密钥并委托 Runtime 实现解密；选错格式明确拒绝。Nova / Pipify 入口在构建前校验配对，明确选择的错误配对不会自动纠正。客户端自动使用 `NovaFramework.Runtime.ResourceManifestDecryptor` 识别格式。明文清单可使用官方 None。Pipify 的三个字段后只保留一个简短说明框，Nova 清单解密候选只显示这两个构建类型。清单加密不覆盖 `.version`、`.hash` 或构建报告。清单自定义压缩/加密器仍需自行配置对应运行时实现。

`ResourceManifestDecryptor` 本身不保存密钥。构建适配通过 `(null, new EditorResourceKeyProvider())` 显式注入，从激活且已保存的 ConfigMaster 取密钥；客户端的包名构造入口读取 `ResourceKeyRegistry.Provider`。两种来源互不覆盖，不要求 Editor 构建先运行客户端启动方法。显式注入时包名为 null 表示从文件头读取后继续认证，客户端显式包名仍严格校验。构建直接使用所选 AES/XOR 解密器，不保留旧通用构建类或类名映射；Runtime 解密器只提供带参数的构造，不提供构建工具无参入口。

Bundle Builder 与 Pipify 的三个下拉共用 `YooAsset.Editor.EditorAssemblyUtility.GetBuildServiceTypes`：排除抽象类、未闭合泛型和没有公开无参构造的类型，None 优先，其余按全类型名排序。Runtime 通用清单解密器需要构造参数，因此不在候选中；其他可实例化的自定义实现仍可选。`ResourceBundleEncryptor`、`ResourceManifestEncryptor` 是算法实现的抽象基类，不是独立方案。通用 `GetAssignableTypes` 仍保留完整派生类型集合，不影响 Collector 规则与其他调用方。

## 原始文件、中间文件与发布文件

默认构建缓存目录为 `{BuildOutputRoot}/{BuildTarget}/{PackageName}/OutputCache`。以 `ui.bundle` 为例：

| 文件 | 内容 | 用途 |
|---|---|---|
| `OutputCache/ui.bundle` | 未加密的原始构建输出 | 保留供构建与缓存使用 |
| `OutputCache/ui.bundle.encrypt` | 加密后的 Bundle | 构建中间文件，与原始文件同目录 |
| 版本目录中的 Bundle | 从 `.encrypt` 复制的密文 | 放入首包或上传 CDN |

发布文件的名字由 `FileNameStyle` 决定：`BundleName` 保留 `ui.bundle`；`HashName` 为 `<密文哈希>.bundle`；`BundleName_HashName` 为 `ui_<密文哈希>.bundle`。因此发布文件仍可叫 `.bundle`，其内容已经加密；`.encrypt` 只是缓存文件后缀。

复制到版本目录后，`.encrypt` **不会立即自动删除**。`ClearBuildCacheFiles` 默认关闭；开启后，下次构建准备阶段删除该平台、该包的整个输出根目录，包括 OutputCache 与历史版本目录，SBP 还会清理 Unity BuildCache。发布取版本目录中的清单和资源文件，不能把 OutputCache 当发布目录。

## RawBundle 与 ArchiveBundle

| 类型 | 内容 | 加密后的读取方式 |
|---|---|---|
| Unity AssetBundle | Unity 构建的资源包 | 原生平台自动解密流；WebGL 内存解密 |
| RawBundle | 单个原始文件，如 JSON、音频或二进制文件 | 内存解密 |
| ArchiveBundle | 多个原始文件及各文件的路径、偏移、长度索引 | 内存解密 |

RawBundle 与 ArchiveBundle 都不是 Unity AssetBundle，也不是不同加密等级。两者在原生平台和 WebGL 均只支持内存解密；即使文件采用 AES stream 格式，也没有流式读取的内存收益。

## 构建与加载链

- 构建从官方默认 `OutputCache` 的父目录确定包名；CLI 与窗口无需共享当前选中状态。自定义输出目录布局不支持该包名解析，需自定义加密器。
- 内容、模式、包名、keyId 及实际密钥都不变时，还原并比较现有中间文件后复用密文；AES 还会验证认证信息，Offset/XOR 没有密码学认证。保持随机 IV，不为增量构建复用固定 IV。清理缓存或删除中间文件会重新生成密文。
- 文件头记录版本、模式、包名、keyId、明文长度和分块信息；不包含真实密钥。
- Runtime `AssetManager` 的 Offline、Host、内置回退均注入 Nova 解密适配。未加密 Bundle 保持官方路径；普通清单原样解析。
- 原生 AssetBundle 默认通过 `ResourceBundleAutoDecryptor`，由 YooAsset 调用 Unity 的 `AssetBundle.LoadFromStream` / `AssetBundle.LoadFromStreamAsync`；AES memory/XOR 先整包解密再包装内存流，AES stream 仅持有当前认证块。Offset 默认使用 `ResourceOffsetStream` 去头流；`ResourceBundleOffsetDecryptor` 与 `GetOffset` 可供自定义加载链接入文件偏移 API，默认链没有使用该路径。
- 流由 YooAsset 持有，到 Bundle 卸载时释放。认证或 I/O 读取故障立即关闭流；Unity 加载 API 抛出且未接管流时，终结器仅提供非即时清理兜底。
- WebGL 密文 AssetBundle 使用内存解密，不走默认 Unity AssetBundle 缓存链，浏览器 HTTP 缓存是独立机制。
- Android 内置加密 Bundle 先由官方默认策略解包到文件系统，再解密读取，须实测首包解包时间和磁盘空间。

## 密钥与客户端代码

框架不内置真实密钥。项目组在 Config 中配置密钥，导出器把密钥作为 Base64 字符串写入 `ResourceKeys.Generated.cs`，客户端从代码解码取得密钥；这是随客户端分发的本地来源，无需服务器。Base64 是编码，客户端密钥可被逆向提取。

`keyId` 是项目组给密钥起的编号，例如 `v1`。文件头记录 PackageName/keyId，`IResourceKeyProvider.GetKey(packageName, keyId)` 按这两个值取对应密钥副本，解密器不用猜密钥。生成代码在 SubsystemRegistration 注册，Preserve 与同目录 `link.xml` 保留提供器及启动方法。

源码须属于随 Player 编译的程序集；导出和构建按 CompilationPipeline 的实际目录归属检查，不能放在 Editor、测试或 HybridCLR 热更程序集，也不能依赖热更程序集。Manifest 和 ConfigRuntime 资源均可能在业务热更 DLL 加载前需要密钥，因此真实密钥不写入 ConfigRuntime。建议使用独立 `ResourceKeys` 子目录，避免与业务手写 `link.xml` 冲突。改变源码目录会清理本工具跟踪的旧产物，保留手写文件。

同一 Unity 项目只允许一份实际生成的密钥提供器。多个 ConfigMaster 可以预存不同导出路径；未配置密钥且未生成过代码时，导出不生成空提供器。需要启用多个 Master 的密钥时，应共用一个正式生成位置，或先处理另一份提供器再导出；每次只导出当前 Master 的密钥集合，不自动合并其他 Master。各 Demo 约定目标为 `Assets/Samples/<Demo>/Scripts/Runtime/ResourceKeys/ResourceKeys.Generated.cs`，配置字段存其目录，不表示所有 Demo 都已生成文件。

团队和 CI 只读取激活且已保存的 ConfigMaster，不再读取独立 JSON 或环境变量。配置改变后先保存 Config，再使用顶部导出、Pipify Config 导出或 `ResourceKeyCodeGenerator.GenerateForCI` 生成源码并等待编译；Player 构建前验证配置、源码、裁剪声明和程序集归属。项目真实密钥保留在项目私有仓库，不放入框架公开仓库或发布包，不打印到日志。

正常发布的 Sample 默认不启用资源加密，也不携带资源密钥；需要演示加密时，由项目组在自己的 Config 中配置并生成。npm 与 GitHub 共用的公开脱敏器只处理临时包副本或公开副仓：清空 `ResourceEncryption.Packages`、`ClientCodeOutputDirectory`、`GeneratedCodeAssetPath`，不写 `YOUR_*` 占位密钥，不回写消费项目的私有配置，也不删除框架加解密能力。

公开副本中，文件起始带 `// 项目生成的资源密钥：` 且完整匹配当前生成格式的提供器及其 `.meta` 会被移除，不依赖文件名或导出目录。带 `<!-- Nova ResourceKeys -->` 标记且仅包含对应 `ResourceKeys` 类型保留声明的工具 `link.xml` 及其 `.meta` 一并清理；手写文件保留，混合源码、混合 linker 或未知生成格式会阻断公开链路。strict 还检查整个副本的非空 `KeyBase64`、资源密钥字面量及已知密钥材料残留，拒绝仍选择 Nova AES/XOR 有密钥构建方案的序列化配置，只报告路径或字段。dry-run 只报告计划，检查失败不应用清理。上述是框架发布副本的处理边界；消费项目继续保留自己的私有配置和生成文件，不依赖开发仓的发布规则文件执行日常配置、生成或构建。

缺少包名、编号或合法密钥时明确失败，不试其他密钥。AES 完整性认证不等于发行方数字签名；本功能未实现数字签名、远端密钥发放、反回滚或防逆向。

## 密钥轮换

例如 Default 包的首包与旧缓存使用 `v1`，后续热更改为 `v2`：客户端提供器同时保留 `Default/v1` 和 `Default/v2`。读首包时取 v1，读新资源时取 v2；“当前加密编号”只控制下一次构建，不改变旧文件需要的密钥。

重新构建全部资源仍需确认所有资源都已使用新密钥，并且新清单、内置首包、缓存、旧版本回退都不再使用 v1，才能在后续客户端移除 v1。只重新编译客户端不会重新加密资源。不要在同一 keyId 下替换密钥，否则旧资源无法解密。

## API 与验证

项目组可通过 `nova.project.config.generate-resource-keys` 独立生成指定已保存私有 Master 的源码，无需先激活该 Master 或创建 Pipify Batch。请求仅包含 `masterGuid` 与明确的 `platform/channel/developMode`；`platform` 必须对应 Unity 当前 Active BuildTarget，渠道不能为 `None`，输出目录和密钥来自保存配置。Plan 只读冻结完整配置和 meta、目标源码/link、旧工具产物迁移、必要新目录及实际 Player 编译图；Execute 单次调用现有生成器，只保存该 Master 的生成路径状态；本次编译及脚本重载后 Verify 核对完整状态、源码/link、删除结果、唯一 provider 与真实程序集。它不导出 ConfigRuntime，不切换 Master 或 Unity 平台，也不返回密钥或生成正文。空密钥且无生成历史返回 `not_applicable`；脏资产/窗口草稿、非私有输出、手写冲突、多个 provider 或计划漂移均阻断。精确旧工具 asmdef 的删除会改变程序集归属时，独立 Action 的只读计划阻断该旧格式分支，先用 ConfigWindow 顶部导出完成一次迁移，再重新计划。执行开始后的失败不会重放，使用 Receipt 只读验收；源码验收不替代实际 Bundle/Player/真机加载。

- `ResourceEncryptionCodec.Encrypt/Decrypt/OpenDecryptionStream/GetOffset`：四种文件模式。
- `NovaFramework.Runtime.ResourceManifestDecryptor`：统一客户端清单解密，支持包名构造和显式密钥提供器注入。
- `NovaFramework.Editor.ResourceManifestAesDecryptor / ResourceManifestXorDecryptor`：对应 AES/XOR 的构建清单解密。
- `GetPackageName/GetMode/GetKeyId`：解析头部，结果在认证前不可信，必须继续解密认证。
- `ResourceKeyStore.Load/Validate/GetActiveKey`：Editor 构建与 CI 从激活 ConfigMaster 同源读取；旧 Save 调用明确拒绝绕过全局保存。
- `ResourceKeyCodeGenerator.Generate/GenerateForCI/ValidateGenerated`：生成与构建一致性门禁。
- [ResourceEncryptionTests](../../../../../Tests/Editor/ResourceEncryptionTests.cs)：四模式、AES 错密钥/缺密钥/篡改、跨文件换块、Seek/EOF/Dispose、密钥模型校验，以及清单解密的密钥来源隔离、包名、明文/null 契约、构建错误配对和候选筛选。
- [ConfigGenerateResourceKeysActionTests](../../../../../Tests/Editor/AgentActions/ConfigGenerateResourceKeysActionTests.cs)：强类型请求、只读预演、私有输出、精确旧产物迁移、手写/唯一 provider 冲突、完整文件漂移、脱敏载荷与未激活已保存 Master 的计划边界。

编辑器测试不替代 Android/iOS IL2CPP 真机的密码学兼容、加载峰值、前后台恢复及性能验证。

相关入口：[ConfigWindow](../../../Editor/Windows/ConfigWindow.md)、[Pipify 构建字段](../../../Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md)、[BundleBuilder](../../../Editor/EditorUtil/EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.md)。

## Runtime 格式与读取边界

`ResourceEncryptionCodec` 只接受 Nova 格式，明文不经过该编码器；`ResourceManifestDecryptor` 才提供“没有完整 `NOVARES1` 标识则原数组返回”的兼容入口，null 仍抛异常。完整标识存在但版本、模式、包名、长度、密钥或认证不合法时严格失败，不再按普通清单回退。

文件头固定部分为 42 字节，版本为 1，模式为 1～4，随后是严格 UTF-8 的包名和 keyId。标识长度上限各为 4096 字节；包名不得为空白，写入的非 Offset keyId 也不得为空白，均不自动 Trim。读取按 Ordinal 校验预期包名，拒绝负明文长度；只有 AES stream 的 chunk 字段为 65536，其余为 0。`HasMagic` 只比较八字节标识，不验证其余头部。`GetPackageName/GetMode/GetKeyId` 解析成功不代表 AES 认证通过。

Offset 不使用密钥；XOR 只要求非空密钥；AES 主密钥必须恰好 32 字节。加解密不修改调用方传入的资源与密钥数组。运行时先克隆提供器返回的密钥，再派生 AES/HMAC 密钥并清零克隆；不尝试其他 keyId。`ResourceKeyRegistry.Provider` 以 Volatile 读写替换，null 可注销；不会释放旧 provider 或在 SubsystemRegistration 自动清空，provider 自身须支持并发。`ResourceKeyRegistry.GetKey` 只拒绝空密钥并返回副本，AES 长度约束在编码器校验；已创建流冻结的副本不随 provider 替换而变化。

AES stream 构造时认证头部并校验精确物理长度，每次只认证、解密并缓存被读取的块，尚未读取的块尚未验证。块记录由 IV、CBC/PKCS7 密文和 HMAC 构成；认证绑定原始头部、块索引、明文长度、IV 和密文，验证通过后才解密。跨块 `Read` 不是事务：后一块失败前，先前有效块可能已复制到调用方 buffer 并推进 Position；不会返回失败块的明文。内存 `Decrypt` 对大于 `int.MaxValue` 的明文拒绝分配；AES memory 与 XOR 的 `OpenDecryptionStream` 仍会整包读取、解密，再返回内存流，不能据返回 Stream 推断内存峰值降低。

两种只读流均以明文相对位置 Seek，允许越过 EOF，负位置抛 IOException，算术溢出抛异常，定位不触发预读；EOF 读取返回 0。`CanRead/CanSeek` 仅反映 wrapper 尚未释放，不重新检查底层能力。Read 先检查 buffer/范围，再检查释放状态。AES 流遇认证或 I/O 异常立即清零密钥和缓存、关闭底层流；Offset 流遇 I/O 异常关闭底层流。Write/SetLength 始终不支持。Dispose 取得锁后先标记释放再关闭输入；内存回退同时清零其明文数组。底层 Dispose 抛出时不会回滚释放标记；终结器只提供延迟兜底。

`GetOffset` 仅接受包名匹配、长度精确的 Offset 文件。内存 Bundle 解密优先使用 `BundleDecryptArgs.FileData`，没有数据才读 FilePath；自动/流解密器使用 FilePath，读取缓冲建议为 64 KiB。显式注入的清单 provider 固定在该实例，客户端包名构造的实例每次读取当前 registry；显式包名 null 才表示从头部取包名，空串不会代替 null。
