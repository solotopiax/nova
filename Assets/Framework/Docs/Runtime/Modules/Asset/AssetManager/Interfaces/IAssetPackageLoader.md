# IAssetPackageLoader

按精确资源包名加载主资源、子资源、全部资源、原始文件或场景。普通业务通过 [AssetComponent](../../AssetComponent.md) 的 `Nova.Asset.Load*FromPackage*` 门面使用；此可选接口供框架内部和自定义 Manager 实现，不要求业务持有 Manager。

## 原有用法保持不变

[IAssetManager](IAssetManager.md) 没有新增强制成员，原 `LoadSync/Async`、`LoadSubsSync/Async`、`LoadAllSync/Async`、`LoadRawSync/Async`、`LoadSceneSync/Async` 的签名和默认包选择不变。现有自定义 Manager 可以继续只实现旧接口；调用新门面而实现不支持时抛 `NotSupportedException`，不会悄悄切换默认包。

新入口的前两参始终为 `string package, string location`：

| 资源 | 同步 / 异步门面 | 返回所有权 |
| --- | --- | --- |
| 主资源 | `LoadFromPackageSync<T>` / `LoadFromPackageAsync<T>` | `IAssetHandle<T>`，调用方 `Release()` |
| 子资源 | `LoadSubsFromPackageSync<T>` / `LoadSubsFromPackageAsync<T>` | `ISubAssetsHandle<T>`，整批 `Release()` |
| 全部资源 | `LoadAllFromPackageSync<T>` / `LoadAllFromPackageAsync<T>` | `IAllAssetsHandle<T>`，整批 `Release()` |
| 原始文件 | `LoadRawFromPackageSync` / `LoadRawFromPackageAsync` | `IRawFileHandle`，调用方 `Release()` |
| 场景 | `LoadSceneFromPackageSync` / `LoadSceneFromPackageAsync` | `ISceneHandle`，调用方 `await UnloadAsync()` |

异步入口最后可传 `CancellationToken ct = default`；场景在它之前有 `LoadSceneMode mode = LoadSceneMode.Single`。`Single` 会替换已有场景，业务内容场景按实际拓扑选择，不能把默认值当作接入方案。WebGL 继续使用异步入口。

## 前置、失败与取消

`package` 必填且不得为空白，必须与已注册包名精确一致；内置实现对空白抛 `ArgumentException`，对未注册包抛 `InvalidOperationException`。已注册但初始化或清单未就绪时，原样传播底层前置失败，不能把它与未注册包混为同一异常。先沿既有启动链完成 `BootstrapAsync` 和目标包 `LoadManifestAsync(package, ct)`，新入口不自动注册、初始化或刷新清单，也不更改默认包。

内置实现复用原加载核心。主资源、子资源、全部资源和场景的完成判定仍沿用原通道，`IsDone` 或方法返回不保证资源非空或加载成功；调用方需检查实际目标。Raw 通道继续检查底层成功状态，失败抛异常。没有增加一套统一的错误 DTO 或重试策略。

显式包异步入口在调度前检查预取消；加载中取消或异常时，内置实现释放未交付句柄。调用方只释放实际收到并接管的 Handle，不能重复释放内部资源；取消不承诺停止所有底层 I/O。成功交付后，Handle 的长期持有与释放责任与旧入口相同。

## 业务接入示例

以下 `contentPackage`、`dataLocation`、`ct` 来自项目已确认的配置与业务生命周期；不需要修改全局默认包。短期读取文本时可以先取得托管字符串再释放资源：

```csharp
IAssetHandle<TextAsset> handle =
    await Nova.Asset.LoadFromPackageAsync<TextAsset>(contentPackage, dataLocation, ct);
string text;
try
{
    if (!handle.IsValid || handle.Asset == null)
        throw new InvalidOperationException("指定资源包的文本不可用。");
    text = handle.Asset.text;
}
finally
{
    handle.Release();
}
```

需要跨帧显示 Sprite 等 Unity 资源时，把完整 Handle 保存在实际显示拥有者，先移除图片引用再释放，不能套用示例的短期文本读取。UI、Prefab 和图片绑定的现有便利入口仍沿各自默认包路径，新增底层选包能力不会自动为它们增加包选择参数。

## 验收路径

在当前项目创建或复用两个真实资源包，以相同地址放置不同内容：分别加载并核对内容，确认旧入口仍读取原默认包；再覆盖空白/缺包、目标清单未就绪、预取消、加载中取消、长期持有释放及场景卸载。编译或替身测试不证明真实 Bundle、Player 或设备加载。

来源：[IAssetPackageLoader.cs](../../../../../../Scripts/Runtime/Modules/Asset/Managers/AssetManager/Interfaces/IAssetPackageLoader.cs)、[AssetComponent.cs](../../../../../../Scripts/Runtime/Modules/Asset/AssetComponent.cs)、[主资源加载实现](../../../../../../Scripts/Runtime/Modules/Asset/Managers/AssetManager/Implements/AssetManager.Load.cs)。
