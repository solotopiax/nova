# AssetBundleBuildProcessor（旧类名说明）

`AssetBundleBuildProcessor` 已不在当前源码中。

当前 Nova 的 AB 构建事实入口已经迁移到：

- [EditorUtil.BundleBuilder.md](../../EditorUtil/EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.md)

当前对应实现代码位于：

- `Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.cs`
- `Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.Methods.cs`
- `Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.BundleBuilder/AssetBundleBuildArgs.cs`

如果你现在要处理的任务是：

- **YooAsset ScriptableBuildPipeline 资源包构建**
  直接看 `EditorUtil.BundleBuilder`。
- **Pipify 中的 AB 构建 Step**
  看 `EditorUtil.Pipify/PipifySteps.md` 里的 `assetbundle.build`。
- **平台构建前后处理 / Manifest / ProGuard / Xcode 工程修改**
  这不是旧 `AssetBundleBuildProcessor` 的职责，应该转去 `Assets/Framework/Scripts/Editor/BuildProcessor/**`。

这个页面保留仅用于兼容旧索引和旧链接，不再代表当前实现事实。
