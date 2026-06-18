# AssetCallbacks

当前 Runtime 资源系统**不再**以全局委托集合 `AssetCallbacks.cs` 作为主契约。

## 当前事实

- 旧式 `PrefabLoadOverCallback`、`AssetLoadOverCallback`、`AssetBundleLoadOverCallBack` 这套回调委托已不再是当前代码的一部分。
- 现在的资源 API 以 `UniTask + Handle` 为主：
  - 主资源：`IAssetHandle<T>`
  - 子资源：`ISubAssetsHandle<T>`
  - 全资源：`IAllAssetsHandle<T>`
  - 原始文件：`IRawFileHandle`
  - 场景：`ISceneHandle`
  - 下载：`IAssetDownloader`

## 迁移后的调用模型

- “完成通知”由 `await` 表达
- “资源持有期”由各类 Handle 表达
- “释放责任”由调用方显式 `Release()` 或 `UnloadAsync()` 承担

## 应该看什么

- [IAssetManager.md](AssetManager/Interfaces/IAssetManager.md)
- [IAssetHandle.md](AssetManager/Interfaces/IAssetHandle.md)
- [ISubAssetsHandle.md](AssetManager/Interfaces/ISubAssetsHandle.md)
- [IAllAssetsHandle.md](AssetManager/Interfaces/IAllAssetsHandle.md)
- [IRawFileHandle.md](AssetManager/Interfaces/IRawFileHandle.md)
- [ISceneHandle.md](AssetManager/Interfaces/ISceneHandle.md)
- [IAssetDownloader.md](AssetManager/Interfaces/IAssetDownloader.md)
