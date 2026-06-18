# DataReceiver

**类签名**：`public abstract class DataReceiver : IDataReceiver`  
**命名空间**：`NovaFramework.Runtime`

面向 `TextAsset` 的通用数据接收基类。它不绑定具体资源模块，而是通过构造函数注入“加载资源 / 释放资源”委托，把“拿到 `TextAsset`”与“如何解析内容”彻底拆开。

## 文件组成

| 文件 | 作用 |
|---|---|
| `DataReceiver.cs` | 基类实现：加载、解析、释放、并发防重入 |
| `IDataReceiver.cs` | 对外契约：`ReadDataAsset` / `ReadDataAssetAsync` / `ReadDataAssetSync` |

## 委托与字段

| 名称 | 类型 | 说明 |
|---|---|---|
| `LoadAssetAsyncFunc` | `UniTask<Object> (string assetLocation)` | 异步加载资源委托 |
| `ReleaseAssetAction` | `void (object asset)` | 释放资源委托 |
| `LoadAssetSyncFunc` | `Object (string assetLocation)` | 同步加载资源委托 |
| `m_ReadTcs` | `UniTaskCompletionSource<bool>` | 异步加载中的共享完成源，用于防重复触发 |

## 构造方式

```csharp
protected DataReceiver(
    LoadAssetAsyncFunc loadAssetAsyncFunc,
    ReleaseAssetAction releaseAssetAction)

protected DataReceiver(
    LoadAssetSyncFunc loadAssetSyncFunc,
    ReleaseAssetAction releaseAssetAction)
```

- 异步路径和同步路径是两组独立构造器。
- 只传异步构造器时，调用 `ReadDataAssetSync` 会抛异常。
- 只传同步构造器时，调用 `ReadDataAsset` / `ReadDataAssetAsync` 会抛异常。

## 公开 API

```csharp
public virtual void ReadDataAsset(string assetLocation)
public virtual UniTask<bool> ReadDataAssetAsync(string assetLocation)
public virtual bool ReadDataAssetSync(string assetLocation)

public abstract bool OnParseDataAsset(string contentString)
public abstract bool OnParseDataAsset(byte[] contentBytes)

public virtual void OnReleaseDataAsset(object dataAsset)
```

## 运行逻辑

### 异步路径

1. `ReadDataAssetAsync` 先校验 `assetLocation` 与异步委托是否存在。
2. 通过 `Interlocked.CompareExchange` 抢占 `m_ReadTcs`。
3. 若已有进行中的加载任务，则直接返回同一个 `Task`，不会重复发起加载。
4. 资源加载完成后进入 `OnAssetLoadComplete`。
5. 尝试把资源转成 `TextAsset`，失败则记日志并返回 `false`。
6. 解析顺序为：
   - 先调 `OnParseDataAsset(textAsset.bytes)`
   - 若返回 `true`，流程结束
   - 若返回 `false` 或抛异常，再尝试 `OnParseDataAsset(textAsset.text)`
7. 无论解析成功与否，都会在 `finally` 中调用 `OnReleaseDataAsset`。

### 同步路径

1. `ReadDataAssetSync` 通过同步委托拿到 `TextAsset`。
2. 解析策略与异步路径一致：先 bytes，后 text。
3. 解析结束后立即执行释放动作。

## 使用示例

```csharp
private sealed class MyDataReceiver : DataReceiver
{
    public List<MyData> Result { get; private set; }

    public MyDataReceiver(IAssetManager assetManager)
        : base(
            loadAssetAsyncFunc: async location =>
            {
                IAssetHandle<TextAsset> handle = await assetManager.LoadAsync<TextAsset>(location);
                TextAsset asset = handle.Asset;
                handle.Release();
                return asset;
            },
            releaseAssetAction: _ => { })
    {
    }

    public override bool OnParseDataAsset(string contentString)
    {
        if (string.IsNullOrEmpty(contentString))
        {
            return false;
        }

        Result = JsonConvert.DeserializeObject<List<MyData>>(contentString);
        return Result != null;
    }

    public override bool OnParseDataAsset(byte[] contentBytes)
    {
        return false;
    }
}

bool success = await new MyDataReceiver(assetManager)
    .ReadDataAssetAsync("data/mydata_asset");
```

## 注意事项

- `DataReceiver` 假定上游返回的是 `TextAsset`；若委托返回别的资源类型，会直接失败。
- `bytes` 解析并不意味着“失败就中断”；当前实现会继续尝试 `text` 解析。
- `OnReleaseDataAsset` 默认只是调用注入的释放委托；如果子类重写，必须保留“解析后立即释放”的语义。

## 关联文档

- [IDataReceiver.md](IDataReceiver.md)
- [LubanDataReceiver.md](LubanDataReceiver.md)
