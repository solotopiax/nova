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
   - bytes 返回 `false` 或抛异常时，都会继续尝试 `OnParseDataAsset(textAsset.text)`
7. 无论解析成功与否，都会在 `finally` 中调用 `OnReleaseDataAsset`。

资源加载委托或释放回调抛出异常时，本轮全部等待者收到加载或释放失败（取消保留取消终态与令牌，不承诺同一个异常对象），并清除本轮共享完成源；后续调用可以重新加载，不会继续等待已失败但未完成的旧任务。收到非 `TextAsset` 资源时返回 `false`，并归还该资源。完成源由当前读取入口负责完成与清理，解析回调不清除其他读取任务。

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
- 异步合流以 Receiver 实例为单位，不以 assetLocation 分组；不同地址在同一实例的并发调用仍共享正在执行的那轮。完成通知前先清本轮 slot，回调同步重入可开始下一轮；本类没有取消令牌、成功缓存或跨轮数据代际保护。void 入口使用 Forget，不向调用者返回加载结果。
- 构造器只保存加载/释放委托并拒绝 null，不加载资产。资源地址拒绝 null/空串但不额外拒绝空白；解析返回 false 不等同整个请求必然失败，因为 bytes 后仍会 text 回退。派生解析已写入的部分缓存不会由本类回滚，回退可能再次处理相同内容。
- 释放回调的存在不证明上游 Handle 持有到解析结束；上例是专用 Unit 适配链，加载委托已先释放 Handle。通用 Table Binding 链使用独立原始字节副本后释放资源，不能用两条链互相替代资源生命周期证据。

## 关联文档

- [IDataReceiver.md](IDataReceiver.md)
- [LubanDataReceiver.md](LubanDataReceiver.md)
