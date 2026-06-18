# IDataReceiver

**类签名**：`public interface IDataReceiver`
**命名空间**：`NovaFramework.Runtime`

数据接收者接口，定义通过 Asset 地址加载、解析和释放数据资源的统一契约。支持同步触发（fire-and-forget）和异步完整流程两种加载方式，解析回调支持字符串和字节流两种数据格式。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `IDataReceiver.cs` | 接口定义 |

## 公开 API

```csharp
// 触发加载（fire-and-forget）
void ReadDataAsset(string assetLocation);

// 异步加载并解析（返回是否成功）
UniTask<bool> ReadDataAssetAsync(string assetLocation);

// 同步加载并解析（返回是否成功）
bool ReadDataAssetSync(string assetLocation);
```

## 关联文档

- [DataReceiver](DataReceiver.md)
