# EditorUtil.BundleBuilder

**类签名**：`public static partial class EditorUtil.BundleBuilder`
**命名空间**：`NovaFramework.Editor`

YooAsset ScriptableBuildPipeline 资源包构建薄封装。一一对齐 BundleBuilderWindow → ScriptableBuildPipeline 视图的 11 项配置。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|------|------|
| `EditorUtil.BundleBuilder.cs` | `EditorUtil.BundleBuilder` | public API：`BuildAssetBundle` / `GetDefaultPackageVersion` |
| `EditorUtil.BundleBuilder.Visitors.cs` | `EditorUtil.BundleBuilder` | 常量：`c_LogPrefix` |
| `EditorUtil.BundleBuilder.Methods.cs` | `EditorUtil.BundleBuilder` | 私有方法：`ResolveClassName` / `CreateInstanceOrNull` / `ResolveBuiltinShaderBundleName` |
| `AssetBundleBuildArgs.cs` | `AssetBundleBuildArgs` | `[Serializable]` 参数 DTO（11 项字段） |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── BundleBuilder (public static partial class)
```

`AssetBundleBuildArgs` 为同命名空间下独立 sealed class（直接 namespace 顶层定义，不嵌套），便于被 Pipify Step 反射读取并序列化。

---

## §5 完整公开 API

```csharp
/// <summary>
/// 启动一次 YooAsset 资源包构建（ScriptableBuildPipeline）。
/// 失败时抛 InvalidOperationException，message 包含 FailedTask 与 ErrorInfo。
/// </summary>
public static BuildResult BuildAssetBundle(AssetBundleBuildArgs args);

/// <summary>
/// 生成默认包裹版本号（yyyy-MM-dd-totalMinutes）。
/// 与 BuildPipelineViewerBase.GetDefaultPackageVersion 完全一致。
/// </summary>
public static string GetDefaultPackageVersion();
```

**`AssetBundleBuildArgs` 字段（11 项）：**

| 字段 | 类型 | 默认值 | 说明 | 标记 |
|---|---|---|---|---|
| `Target` | `BuildTarget` | `NoTarget` | 目标平台；`NoTarget` 时使用 `EditorUserBuildSettings.activeBuildTarget` | — |
| `PackageName` | `string` | `"Default"` | 包裹名称（**必填**） | — |
| `BuildVersion` | `string` | `""` | 包裹版本号；空字符串时按 `yyyy-MM-dd-totalMinutes` 自动生成 | `[PipifyDynamicDefault(typeof(EditorUtil.BundleBuilder), nameof(EditorUtil.BundleBuilder.GetDefaultPackageVersion))]` |
| `ClearBuildCache` | `bool` | `true` | 是否清理构建缓存 | — |
| `UseAssetDependencyDB` | `bool` | `false` | 是否使用资源依赖数据库 | — |
| `BundleEncryptorClassName` | `string` | `""` | 资源包加密器全类型名；空时回退 `YooAsset.Editor.EncryptionNone` | `[PipifyDropdown(typeof(IBundleEncryptor))]` |
| `ManifestEncryptorClassName` | `string` | `""` | 资源清单加密器全类型名；空时回退 `YooAsset.Editor.ManifestEncryptorNone` | `[PipifyDropdown(typeof(IManifestEncryptor))]` |
| `ManifestDecryptorClassName` | `string` | `""` | 资源清单解密器全类型名；空时回退 `YooAsset.Editor.ManifestDecryptorNone` | `[PipifyDropdown(typeof(IManifestDecryptor))]` |
| `Compression` | `ECompressOption` | `LZ4` | 压缩方式（`Uncompressed` / `LZMA` / `LZ4`） | — |
| `FileNameStyle` | `EFileNameStyle` | `BundleName_HashName` | 远端资源文件命名风格 | — |
| `BundledCopyOption` | `EBundledCopyOption` | `ClearAndCopyAll` | 首包资源拷贝选项 | — |
| `BundledCopyParams` | `string` | `""` | 首包资源拷贝标签（仅按标签拷贝时生效） | `[PipifyVisibleWhen(nameof(BundledCopyOption), (int)ClearAndCopyByTags, (int)OnlyCopyByTags)]` |

**Pipify 渲染特性（PipifyWindow 解析，BundleBuilder 自身不依赖）：**
- `PipifyDropdown(InterfaceType)`：将 string 字段渲染为接口实现类下拉框，存储 `Type.FullName`，首项 `(未配置 → Default)` 表示空串。
- `PipifyDynamicDefault(ProviderType, MethodName)`：值为空时调用指定无参 static 方法获取占位字符串显示，不写回字段值。
- `PipifyVisibleWhen(DependsOn, params int[] AnyOf)`：依赖字段当前 `Convert.ToInt32` 值命中 AnyOf 时才显示。

**异常：**
- `ArgumentNullException`：`args` 为 null。
- `ArgumentException`：`PackageName` 为空。
- `InvalidOperationException`：`BuildResult.Success == false`。

**固定行为（与 BundleBuilder 窗口对齐，不外暴露）：**
- 输出根目录 = `BundleBuilderHelper.GetDefaultBuildOutputRoot()`（项目根 `/Bundles`）
- BundledFileRoot = `BundleBuilderHelper.GetStreamingAssetsRoot()`
- BuildPipeline = `EBuildPipeline.ScriptableBuildPipeline`
- BuildBundleType = `EBundleType.AssetBundle`
- `EnableSharePackRule = true` / `VerifyBuildingResult = true`
- `BuiltinShadersBundleName` 由 `BundleCollectorSettingData.Setting.UniqueBundleName` + `DefaultBundlePackRule.CreateShadersPackRuleResult()` 推导

---

## §11 使用示例

```csharp
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
```

Pipify Step `assetbundle.build` 直接复用 `AssetBundleBuildArgs` 作为参数类。

---

## §13 关联文档

- [EditorUtil.md](../EditorUtil.md)
- [EditorUtil.Build.md](../EditorUtil.Build/EditorUtil.Build.md)
- [PipifySteps.md](../EditorUtil.Pipify/PipifySteps.md)
