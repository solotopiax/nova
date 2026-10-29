# EditorUtil.BundleBuilder

**类签名**：`public static partial class EditorUtil.BundleBuilder`
**命名空间**：`NovaFramework.Editor`

YooAsset 标准 AssetBundle 与可选 RawFile 构建薄封装。两个入口使用独立 DTO、构建参数和 Pipify Step，互不改变对方语义。

标准 `BuildAssetBundle` 在执行管线前，对当前 Sample 工作区增加只读 DLL 目标检查：按本次 BuildTarget 与激活 Master 的 Channel/DevelopMode 解析映射，仅核对所选 Package 中 `NormalIgnoreRule`、`EnableGroup`、`MainAssetCollector`、`CollectAll` 覆盖的目标，要求文件非空且已作为 `TextAsset` 导入。缺失时明确失败，提示完成相应平台的生成、编译与复制；导入时准备的空目录不能替代 DLL。当前发布的全部 Sample 使用这组默认规则。自定义规则继续由原管线处理，此预检不额外执行其回调；其他 Package、普通业务工作区、RawFile、Editor 模拟构建与 Player 首次生成 AOT 不增加该先决条件。AOT 副本仍应在相应 Player 构建产生裁剪产物后复制。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|------|------|
| `EditorUtil.BundleBuilder.cs` | `EditorUtil.BundleBuilder` | public API、默认版本号、首包拷贝校验与 WebGL `None` 旧目录清理 |
| `EditorUtil.BundleBuilder.Visitors.cs` | `EditorUtil.BundleBuilder` | 日志常量 |
| `EditorUtil.BundleBuilder.Methods.cs` | `EditorUtil.BundleBuilder` | 私有方法：`ResolveClassName` / `CreateInstanceOrNull` / `ResolveBuiltinShaderBundleName` |
| `AssetBundleBuildArgs.cs` | `AssetBundleBuildArgs` | `[Serializable]` 标准 AssetBundle 构建参数 DTO |
| `RawFileBuildArgs.cs` | `RawFileBuildArgs` | `[Serializable]` RawFile 构建参数 DTO |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── BundleBuilder (public static partial class)
```

`AssetBundleBuildArgs` 与 `RawFileBuildArgs` 均为同命名空间下的独立 sealed class，便于 Pipify Step 反射读取并序列化。

---

## §5 完整公开 API

```csharp
/// <summary>
/// 启动一次 YooAsset 资源包构建（ScriptableBuildPipeline）。
/// 失败时抛 InvalidOperationException，message 包含 FailedTask 与 ErrorInfo。
/// </summary>
public static BuildResult BuildAssetBundle(AssetBundleBuildArgs args);

/// <summary>
/// 使用 RawFileBuildPipeline 启动一次原生文件构建。
/// </summary>
public static BuildResult BuildRawFileBundle(RawFileBuildArgs args);

/// <summary>
/// 生成默认包裹版本号（yyyy-MM-dd-totalMinutes）。
/// 与 BuildPipelineViewerBase.GetDefaultPackageVersion 完全一致。
/// </summary>
public static string GetDefaultPackageVersion();
```

**`AssetBundleBuildArgs` 字段：**

| 字段 | 类型 | 默认值 | 说明 | 标记 |
|---|---|---|---|---|
| `Target` | `BuildTarget` | `NoTarget` | 直接 API 可显式指定目标；`NoTarget` 时使用 `EditorUserBuildSettings.activeBuildTarget`。Pipify 中始终同步当前 Active BuildTarget 且只读 | `[PipifyReadOnly]` |
| `PackageName` | `string` | `"Default"` | 包裹名称（**必填**） | — |
| `BuildVersion` | `string` | `""` | 包裹版本号；空字符串时按 `yyyy-MM-dd-totalMinutes` 自动生成 | `[PipifyDynamicDefault(typeof(EditorUtil.BundleBuilder), nameof(EditorUtil.BundleBuilder.GetDefaultPackageVersion))]` |
| `ClearBuildCache` | `bool` | `true` | 是否清理构建缓存 | — |
| `UseAssetDependencyDB` | `bool` | `false` | 是否使用资源依赖数据库 | — |
| `BundleEncryptorClassName` | `string` | `typeof(EncryptionNone).FullName` | 资源包加密器全类型名；空时同样回退 `YooAsset.Editor.EncryptionNone` | `[PipifyDropdown(typeof(IBundleEncryptor))]` |
| `ManifestEncryptorClassName` | `string` | `typeof(ManifestEncryptorNone).FullName` | 资源清单加密器全类型名；空时同样回退 `YooAsset.Editor.ManifestEncryptorNone` | `[PipifyDropdown(typeof(IManifestEncryptor))]` |
| `ManifestDecryptorClassName` | `string` | `typeof(ManifestDecryptorNone).FullName` | 资源清单解密器全类型名；空时同样回退 `YooAsset.Editor.ManifestDecryptorNone` | `[PipifyDropdown(typeof(IManifestDecryptor))]` |
| `Compression` | `ECompressOption` | `LZ4` | 压缩方式（`Uncompressed` / `LZMA` / `LZ4`） | — |
| `FileNameStyle` | `EFileNameStyle` | `BundleName_HashName` | 远端资源文件命名风格 | — |
| `BundledCopyOption` | `EBundledCopyOption` | `ClearAndCopyAll` | WebGL 允许 `None` / `ClearAndCopyByTags` / `ClearAndCopyAll`；其他平台保留 YooAsset 原选项 | — |
| `BundledCopyParams` | `string` | `""` | 按 Tag 拷贝时的标签；多标签以分号分隔 | `[PipifyVisibleWhen(nameof(BundledCopyOption), (int)ClearAndCopyByTags, (int)OnlyCopyByTags)]` |

**`RawFileBuildArgs` 字段：**

| 字段 | 类型 | 默认值 | 说明 | 标记 |
|---|---|---|---|---|
| `Target` | `BuildTarget` | `NoTarget` | 直接 API 可显式指定目标；`NoTarget` 时使用 `EditorUserBuildSettings.activeBuildTarget`。Pipify 中始终同步当前 Active BuildTarget 且只读 | `[PipifyReadOnly]` |
| `PackageName` | `string` | `"Default"` | 包裹名称（**必填**） | `[PipifyDynamicDropdown(typeof(AssetBundleBuildArgs), nameof(AssetBundleBuildArgs.GetPackageNameOptions))]` |
| `BuildVersion` | `string` | `""` | 包裹版本号；空字符串时生成默认版本号 | `[PipifyDynamicDefault(typeof(EditorUtil.BundleBuilder), nameof(EditorUtil.BundleBuilder.GetDefaultPackageVersion))]` |
| `ClearBuildCache` | `bool` | `true` | 是否清理构建缓存 | — |
| `UseAssetDependencyDB` | `bool` | `false` | 是否使用资源依赖数据库 | — |
| `BundleEncryptorClassName` | `string` | `typeof(EncryptionNone).FullName` | RawBundle 加密器全类型名 | `[PipifyDropdown(typeof(IBundleEncryptor))]` |
| `ManifestEncryptorClassName` | `string` | `typeof(ManifestEncryptorNone).FullName` | 资源清单加密器全类型名 | `[PipifyDropdown(typeof(IManifestEncryptor))]` |
| `ManifestDecryptorClassName` | `string` | `typeof(ManifestDecryptorNone).FullName` | 资源清单解密器全类型名 | `[PipifyDropdown(typeof(IManifestDecryptor))]` |
| `FileNameStyle` | `EFileNameStyle` | `BundleName_HashName` | 远端资源文件命名风格 | — |
| `BundledCopyOption` | `EBundledCopyOption` | `ClearAndCopyAll` | WebGL 允许 `None` / `ClearAndCopyByTags` / `ClearAndCopyAll`；其他平台保留 YooAsset 原选项 | — |
| `BundledCopyParams` | `string` | `""` | 按 Tag 拷贝时的标签 | `[PipifyVisibleWhen(nameof(BundledCopyOption), (int)ClearAndCopyByTags, (int)OnlyCopyByTags)]` |
| `IncludePathInHash` | `bool` | `false` | 计算 RawFile 哈希时是否包含文件路径 | — |

Raw DTO 不包含 `Compression`、`BuiltinShadersBundleName` 等 ScriptableBuildPipeline 专属配置。

**Pipify 渲染特性（PipifyWindow 解析，BundleBuilder 自身不依赖）：**
- `PipifyReadOnly`：禁用该字段编辑。两个 `Target` 字段在 Pipify 中用它只读展示 Unity 当前 Active BuildTarget，执行期会再次同步。
- `PipifyDropdown(InterfaceType)`：将 string 字段渲染为接口实现类下拉框，存储 `Type.FullName`，首项 `(未配置 → Default)` 表示空串。
- `PipifyDynamicDefault(ProviderType, MethodName)`：值为空时调用指定无参 static 方法获取占位字符串显示，不写回字段值。
- `PipifyVisibleWhen(DependsOn, params int[] AnyOf)`：依赖字段当前 `Convert.ToInt32` 值命中 AnyOf 时才显示。

**异常：**
- `ArgumentNullException`：`args` 为 null。
- `ArgumentException`：`PackageName` 为空。
- `InvalidOperationException`：`BuildResult.Success == false`。

**标准 AssetBundle 固定行为：**
- 输出根目录 = `BundleBuilderHelper.GetDefaultBuildOutputRoot()`（项目根 `/Bundles`）
- BundledFileRoot = `BundleBuilderHelper.GetStreamingAssetsRoot()`
- BuildPipeline = `EBuildPipeline.ScriptableBuildPipeline`
- BuildBundleType = `EBundleType.AssetBundle`
- `EnableSharePackRule = true` / `VerifyBuildingResult = true`
- `BuiltinShadersBundleName` 由 `BundleCollectorSettingData.Setting.UniqueBundleName` + `DefaultBundlePackRule.CreateShadersPackRuleResult()` 推导

**RawFile 固定行为：**
- 输出根目录与 BundledFileRoot 使用相同的 YooAsset 默认路径
- BuildPipeline = `EBuildPipeline.RawFileBuildPipeline`
- BuildBundleType = `EBundleType.RawBundle`
- `VerifyBuildingResult = true`
- 当前 RawFile 管线收集所选 Package 的全部主资源，再要求每个 Bundle 只有一个资源；它不自动按 `PackRawFile` 过滤。受控 Action 要求独立 RawFile Package 的有效组仅使用主 Collector 与 `PackRawFile`，混合采集明确阻断。

### WebGL 的首包拷贝规则

`BuildAssetBundle`、`BuildRawFileBundle`、Pipify Step 与受控 Build Action 在 WebGL 下统一支持：

- `None`：清理目标 Package 的旧首包目录，不复制 Manifest/Bundle，运行时使用纯 CDN。
- `ClearAndCopyByTags`：复制 Manifest、Catalog 和指定 Tag 的 Bundle，运行时组合 StreamingAssets 与 CDN。
- `ClearAndCopyAll`：复制 Manifest、Catalog 和全部 Bundle；Host 模式仍保留 CDN 更新能力。

WebGL 不允许 `OnlyCopyAll` / `OnlyCopyByTags`，因为它们可能保留旧文件，使运行时无法准确判断本次构建布局。非 WebGL 平台仍保留 YooAsset 原有五种选项。

WebGL 的 `None` 构建成功后必须删除目标 Package 的旧首包目录，避免遗留 Catalog 被误判为仍有首包。后续 Player 构建会根据输出目录中的官方 `BuiltinCatalog.bytes` 生成 `StreamingAssets/nova-webgl-layout.txt`；运行时读取该清单选择 WebServer/WebNetwork 组合，不再通过 404 探测 Catalog。浏览器何时请求 Bundle 仍由 `WebGLAssetStrategy` 决定，见 [WebGLAssetStrategies.md](../../../Runtime/Modules/Asset/WebGLAssetStrategies.md)。

---

## §11 使用示例

```csharp
// 以下都是直接 API 示例：可显式传 Target；Pipify Step 不接受手工 Target。
// 最小调用：仅必填 PackageName，其他全部默认
BuildResult result = EditorUtil.BundleBuilder.BuildAssetBundle(
    new AssetBundleBuildArgs { PackageName = "DefaultPackage" });
Log.Debug(LogTag.Editor, "产物：{0}", result.OutputPackageDirectory);

// 自定义压缩与命名风格
EditorUtil.BundleBuilder.BuildAssetBundle(new AssetBundleBuildArgs
{
    PackageName = "DefaultPackage",
    Target = BuildTarget.Android,
    Compression = ECompressOption.LZ4,
    FileNameStyle = EFileNameStyle.BundleName_HashName,
    ClearBuildCache = true,
    BundledCopyOption = EBundledCopyOption.ClearAndCopyAll,
});

// 可选 RawFile 构建：目标资源必须使用 PackRawFile
EditorUtil.BundleBuilder.BuildRawFileBundle(new RawFileBuildArgs
{
    PackageName = "RawPackage",
    Target = BuildTarget.Android,
    IncludePathInHash = false,
});
```

- `bundlebuilder.build` 复用 `AssetBundleBuildArgs`，保持 `ScriptableBuildParameters` + `ScriptableBuildPipeline` + `AssetBundle`。
- `bundlebuilder.build_raw_file` 复用 `RawFileBuildArgs`，使用 `RawFileBuildParameters` + `RawFileBuildPipeline` + `RawBundle`。

HybridCLR DLL 不走 RawFile 通道，仍按标准 AssetBundle 构建并通过 `LoadAsync<TextAsset>` 加载。

---

## §13 关联文档

- [EditorUtil.md](../EditorUtil.md)
- [EditorUtil.Build.md](../EditorUtil.Build/EditorUtil.Build.md)
- [PipifySteps.md](../EditorUtil.Pipify/PipifySteps.md)
- [WebGLAssetStrategies.md](../../../Runtime/Modules/Asset/WebGLAssetStrategies.md)

## Nova 资源加密方案

既有 `BundleEncryptorClassName`、`ManifestEncryptorClassName`、`ManifestDecryptorClassName` 字段与官方构建页面同源，不新增方案配置字段。XOR/AES memory/AES stream Bundle 方案及 XOR/AES 清单方案从激活且已保存的 Config 资源加解密配置读取资源包密钥，缺密钥阻断。Offset 仅追加头部混淆，不需要密钥。一次 Package 构建统一使用所选 Bundle Encryptor，不按文件大小自动切换。

清单的 `NovaFramework.Editor.ResourceManifestAesEncryptor` 对应 `NovaFramework.Editor.ResourceManifestAesDecryptor`，`ResourceManifestXorEncryptor` 对应同一命名空间的 `ResourceManifestXorDecryptor`；Nova/Pipify 构建前校验配对。两个构建解密器只接受各自格式，从已保存的 ConfigMaster 取密钥并委托 Runtime 解密；客户端由 Nova 自动配置 `NovaFramework.Runtime.ResourceManifestDecryptor`，读取生成代码注册的提供器。

Pipify 三个字段只保留一个简短说明框。Bundle Builder 与 Pipify 共用 `EditorAssemblyUtility.GetBuildServiceTypes` 候选与排序，排除抽象基类、未闭合泛型和无公开无参构造的实现；Runtime 清单解密器需要构造参数，因此不在候选中，其他可实例化自定义实现仍可选。构建直接实例化所选类型，不保留旧通用构建类或类名映射；明确选择的错误配对不会自动纠正。自定义加密器不自动匹配 Nova 解密器。详见 [资源加解密](../../../Runtime/Modules/Asset/ResourceEncryption.md)。

### 受控 Bundle Action

`nova.project.bundle.build-asset` 与 `nova.project.bundle.build-raw-file` 使用逻辑值白名单，不接受直接 API 中的类型名参数。`encryptionPolicy` 必填，映射如下：

| 逻辑值 | Bundle 加密器全类型名 |
|---|---|
| `none` | `YooAsset.Editor.EncryptionNone` |
| `offset` | `NovaFramework.Editor.ResourceOffsetEncryptor` |
| `xor` | `NovaFramework.Editor.ResourceXorEncryptor` |
| `aes-memory` | `NovaFramework.Editor.ResourceAesMemoryEncryptor` |
| `aes-stream` | `NovaFramework.Editor.ResourceAesStreamEncryptor` |

`manifestEncryptionPolicy` 可选，省略或 `null` 使用 `none`；显式空串、未知值、类型名和别名被拒绝。清单与 Bundle 独立选择：

| 逻辑值 | 清单加密器全类型名 | 构建清单解密器全类型名 |
|---|---|---|
| `none` | `YooAsset.Editor.ManifestEncryptorNone` | `YooAsset.Editor.ManifestDecryptorNone` |
| `aes` | `NovaFramework.Editor.ResourceManifestAesEncryptor` | `NovaFramework.Editor.ResourceManifestAesDecryptor` |
| `xor` | `NovaFramework.Editor.ResourceManifestXorEncryptor` | `NovaFramework.Editor.ResourceManifestXorDecryptor` |

Plan 同时冻结两项逻辑值和三个实际正式类型名；Execute 使用冻结值调用既有 BundleBuilder，沿用已保存 ConfigMaster 的密钥来源和缺密钥阻断规则。计划不携带密钥。既有 `encryptionPolicy=none` 且不传清单参数的请求仍生成明文 Bundle 与清单。该受控入口不提供任意自定义类型或旧通用清单类名兼容。

两个 Action 按当前正式参数类冻结共享 `OutputCache`；`clearBuildCache=false` 仍会写该目录。AssetBundle 另声明 `Library/PlayerScriptAssemblies`、`Temp/ContentBuildData`、`Library/BuildCache` 与 `Library/Bee`；启用依赖数据库时声明 `Library/AssetDependencyDB`。清缓存会删除整个选中 Package 根及其全部版本，AssetBundle 还会清全局 SBP 缓存。共享缓存可被后台裁剪，WriteSet 不代表缓存完整性证据。

Plan 冻结 SBP Scene/Shader/Compute 回调、属性回调与四个 SBP 委托的编译身份；自定义回调缺少确定写入契约、已有脏资产可能被全局 `SaveAssets` 保存、或 SBP 临时输出已存在时返回 `blocked`，不实例化回调或删除残留。Execute 重新核对，不能用确认绕过门禁。未知回调逐项补契约后再开放，不通过扩大 `Assets/**` 写集代替审查。

Firebase Crashlytics 的 Android Scene 回调按当前正式包与两个 Editor DLL Hash 核对。Plan 不读取 `Firebase.Editor.Measurement.analytics`，避免首次初始化在只读阶段整理标签；它冻结 `l:gvh` 查询的实际存在路径、可能读取的 manifest 全部非空行、GUID、源文件、原标签和 meta。可写 metadata 必须位于 `Assets`，仅允许正规标签结果，meta 除唯一顶层 `labels` 块外的原字节必须保持。包候选按 EDM 精确包路径规则只读；未知路径、受管标签或 meta 布局明确阻断。统计的同意选择须已完成，Enabled 使用当前值，不关闭开关。

正常回调声明 `Assets/Plugins/Android/FirebaseCrashlytics.androidlib` 下四个文件、必要目录和对应 meta：`AndroidManifest.xml`、`project.properties` 仅缺失时创建，两个 `res/values/crashlytics_*.xml` 按原回调生成。Execute 只调用一次原构建流程，作用域采集两个资源成功行和包括普通 Log 级别在内的写失败，回执保留真实前后文件、标签、`Generated` 与变化路径。`Generated=true` 只表示原回调到达末尾，不能替代成功日志或文件语义；重复构建的已生成跳过分支保持原文件，不重置标志、不要求生成新 GUID。取消或异常保留未完成回执与恢复核查。Verify 同时核对本次日志、XML 的 GUID/Unity 版本、仅创建文件的保持规则和标签边界；旧合法文件与 Console 无 Error 均不能证明本次生成。当前已审 `Report(string,string)` 不发送网络数据。

IAP 已存在 `BillingMode.json` 时，按原 `AppStore` 大小写敏感判定、未知值回退 GooglePlay 和精确 importer 选择规则冻结目标；只有当前 Android 兼容值已满足目标时才接受。无唯一 fallback 候选保持原回调无写分支；需要实际改变 importer 的分支仍有具体门禁，不修改配置消除阻断。

SBP 的场景恢复会从磁盘重新加载原场景，因此 Plan 要求当前场景有保存路径、无脏数据，并冻结场景 setup。有效 Collector 根与所有递归依赖采用保守闭包，不执行采集规则或打开场景；记录逻辑路径、GUID、依赖 Hash、实物和 `.meta` SHA-256，以及 PlayerSettings、GraphicsSettings、QualitySettings。没有磁盘文件的 Unity 内置依赖只记录逻辑身份。执行前后重新核对闭包，不能仅凭原场景列表或文件未删除证明恢复。

当前已审 SBP 分支按引擎 DLL 或正式包版本及源码 Hash 精确匹配：UnityBuildPostprocessor 的场景静态合批同时冻结当前目标的 static batching 设置；ProjectAuditor、Timeline、UGUI 的 SBP 分支只处理内存数据。Adaptive Performance 的 Scene 分支冻结 `UnityEditor.EditorAnalytics.enabled`，已启用时会按非空报告发送构建 GUID、目标平台/平台版本和 provider 信息；`callbacks.knownEffects` 展示这些原外部效果，Action 正式声明 `ExternalWrite`。当前仅接受已审 metadata 的空 provider 字典，非空集合仍需逐实现核对 getter；不自动关闭 Analytics。引擎的 `AttributeCallbackWrapper` 是容器，属性方法仍单独冻结；其注册条件沿用引擎的静态、非泛型及精确参数规则。

三项 Shader 回调还冻结固定版本的内部报告器与事件字段：报告器仅接受已审的内存或日志实现，发现未审可实例化的内部 `IVariantStripper` 或事件订阅便阻断，不调用构造函数或委托。当前 `SRPDisabledComputeShaderVariantStripper` 仅在全部 Quality/default Graphics 无 SRP，且 `CoreBuildData` 未初始化或已有无 SRP 空 Compute 缓存时接受；只读固定字段，不调用 lazy getter。SRP 资产的虚拟 getter 与 GPU 接口仍未放行。ProBuilder 还要求正式 Settings 实例已经初始化，避免首次静态 Pref 初始化迁移旧配置和注册保存回调；Plan 仅读无类型初始化器的固定字段，不调用设置 getter。仅接受已加载对象和上述源依赖闭包均无 `ProBuilderMesh` / `EntityBehaviour` 的空分支；含这些组件的输入仍需独立审查 Mesh 资产缓存等副作用。所有已审契约只适用于 SBP 的 Scene/Shader/Compute 路由，不据此放行 Player 构建回调。

Collector 的忽略、组激活、寻址、打包与过滤规则也会执行代码。Action 只接受已核对的 YooAsset 默认规则，并冻结其程序集/模块身份；同名具体类型冲突或自定义规则缺少写入契约时阻断，不在 Plan 实例化任意规则。直接 API 与 Pipify 的自定义能力保持原语义，消费态受控 Action 的可执行范围需经过独立契约补齐。

Receipt 保留共享写集和 OutputCache、脚本输出及可选依赖数据库的执行后 Hash；Verify 只读校验完整辅助集合、本次正式报告的 Target/Package/Version/管线/Bundle 类型及报告引用文件，再核 SHA-256。后续构建改变共享输出会使旧 Receipt 返回 `partial`；旧 Bundle Receipt 缺少共享证据也为 `partial`。Player 原 Receipt 保持原验证边界。进入构建后的失败保留未完成 Receipt 与可能写入路径，不自动重跑或清原缓存。

AssetBundle 成功与失败 Receipt 均记录执行前源文件、场景 setup 和未保存状态的只读核查结果；存在漂移时不能形成成功凭据。该核查不会替用户保存或恢复场景与资产，失败仍保留现场供归属核查。

Framework Editor 为冻结当前公开 SBP 路径与委托 API，直接引用 `Unity.ScriptableBuildPipeline.Editor`；Framework `package.json` 显式声明 `com.unity.scriptablebuildpipeline` 当前版本下界。消费工程会通过 UPM 解析该既有构建依赖，不需要 Agent 安装临时工具。

IAP 场景回调在 `Assets/Resources/BillingMode.json` 不存在时按已审构建方法的空分支接受；存在时按原 `AppStore` 大小写敏感判定与未知值回退 GooglePlay 的规则，冻结 BillingMode Hash、canonical importer 或精确同名 fallback 候选集合。没有唯一 importer 时原方法无配置写；有 importer 仅在 Android 兼容性已满足目标值时接受，并冻结实际插件及 `.meta` Hash。需要改变配置时仍阻断，不能调用 IAP 设置 API 代替回调或触发其延迟初始化。

EDM 警告回调在非 Android 下为空分支；Android 下冻结原三个警告/自动解析设置，沿正式 EditorSettings / InMemorySettings 的已加载状态只读捕获，不调用私有设置 getter或强制加载 XML。该回调只保留原 Console 警告和内存 HasWarned 状态；真正自动 Resolve 仍需要独立完整的依赖解析合同。

Android EDM 场景无需解析分支还冻结完整依赖图、解析状态与产物、工具和主线程调度。首次必须有本次四条有序场景事件及计数增长，后续仅接受无解析事件的正常计数增长；不重置计数或主动执行解析。为了观察被 Console 过滤的详细日志，作用域临时订阅已审公开 Logger 尾部事件，只保留固定 token/失败类别，精确退订自身并核对原委托；日志级别不变。正规 Jetifier 前置仅接纳项目已有 True 且 canonical XML 与原字节相同的分支，计划声明精确 Gvh 文件和 `Unity EditorPrefs:GooglePlayServices.UseJetifier` 同值写，其他设置保持。Verify 不预读 XML、不重放回调；缓存命中可走独立证据分支：只有本次写出的精确报告和完整 SBP trace、场景 GUID 与冻结依赖闭包、全部场景/写操作缓存标记和计数一致，且场景计数、事件、EDM/Firebase 完整状态和写入集合均未变化，才接受真实未重跑场景回调的缓存结果。缺少 trace、混合未缓存场景或域身份改变保持 partial，不升级旧 Receipt。初始与最终各做一次完整 Version Handler 只读采集，中间仅双读绑定实际 DLL/MVID、域、owner/scheduler 身份及原调度不变量的廉价只读 pending 探针。首完整采集与探针绑定后启动唯一固定十秒的自然 Editor update 等待预算，至少观察一次真实 update；探针完成或到期后只做一次最终完整后验，采集可越过截止点但必须完整匹配且自然完成，仍 pending 保留 partial。回执分别保留真实 update 与成功探针计数，二者须相等且只含首末两份完整快照；旧无探针字段回执仍按每次 update 一份完整快照的原规则读取。未知探针或稳定输入变化立即失败，不重开窗口，不推进、调用或清空官方队列。

构建的全局脏资产门另精确排除已导入字体的零纹理缓存：必须是真实 non-native/foreign 子资产、NotEditable、准确且未改动的 TrueTypeFontImporter，唯一已加载同路径准确主 Font 未改动。只读 Font 的 `m_Texture` 实例 ID，必须直接指向现存候选纹理；`m_DefaultMaterial` 只要求原序列化 ID 非零，不解引用或要求 Material 已加载，也不调用 Font.material、Material.mainTexture 或 objectReferenceValue 来制造条件。生产筛选不 LoadAsset、请求字符或调用 FontEngine。纹理 native 只读元数据和完整零缓存 JSON（包括版本、名称、所有数据/stream 空值及全部 key）必须一致，任何未知或非零字段继续阻断。此分类不清 dirty、不保存或重写源字体/meta；Font 本体、Importer、原生资产、PNG、错误归属与未知导入纹理仍受原门保护。它不宣称已审完 native SaveAssets 的 C++ 实现，也不扩大回调写集。

## 调用与失败边界

下拉 GetPackageNameOptions 无法读场景配置时回落 Default，只是 UI 选项，不证明 Collector 已配置。构建实现类按准确 FullName 选首个可赋值类型并 Activator 创建：空名称先用默认 Type.FullName，空白不回落；候选未找到会 Warning/null，构造、静态初始化或强转错误可传播，不做所有同名程序集身份的唯一性审查。

默认版本基于本地日期与总分钟，不保证同分钟唯一。AssetBundle 与 RawFile 参数结构不同，前者有 Compression，后者有 IncludePathInHash；不能借同名字段把两套参数互换。WebGL 只支持 None、ClearAndCopyByTags、ClearAndCopyAll，非 Tag 模式清空 Tags，非 WebGL 不额外校验所有枚举值。成功构建后的 WebGL None 还需删除本 Package 的旧首包资产并 Refresh；清理失败会使 API 抛错，不能因为 BuildResult 先成功就宣称全链完成。
