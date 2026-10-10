# EditorUtil.BundleBuilder

**类签名**：`public static partial class EditorUtil.BundleBuilder`
**命名空间**：`NovaFramework.Editor`

YooAsset 标准 AssetBundle 与可选 RawFile 构建薄封装。两个入口使用独立 DTO、构建参数和 Pipify Step，互不改变对方语义。

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
- 仅处理 Collector 中使用 `PackRawFile` 的目标资源

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
