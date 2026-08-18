# IRawFileHandle

**类签名**：`public interface IRawFileHandle`
**命名空间**：`NovaFramework.Runtime`

原始文件句柄中性接口，适用于以 RawFile 模式打包的二进制资源（DLL 字节、数据文件等）。

---

## 文件

| 文件 | 类 | 说明 |
|------|-----|------|
| `Managers/AssetManager/Interfaces/IRawFileHandle.cs` | `IRawFileHandle` | 接口定义 |

---

## 完整公开 API

```csharp
bool IsValid { get; }     // 句柄是否仍有效（未释放）
bool IsDone { get; }      // 异步加载是否完成
string FilePath { get; }  // 尽力解析的底层资源包文件路径；未完成、无效、已释放或同步/Web/内存场景下可能为 null
byte[] GetBytes();        // 从 RawFileObject 返回原始文件字节副本（未完成、无效或已释放时返回 null）
void Release();           // 释放句柄（引用计数 -1）
```

---

## 使用示例

```csharp
// 加载以 RawFile 模式打包的通用二进制数据
IRawFileHandle handle = await Nova.Asset.LoadRawAsync("data/GameConfig", ct);
try
{
    byte[] bytes = handle.GetBytes();
    // 按业务数据格式解析 bytes。
}
finally
{
    handle.Release();
}
```

异步加载会尽力通过 YooAsset `EnsureBundleFileAsync(...).Detail.BundleFilePath` 补充 `FilePath`；同步操作不支持等待该操作，Web/内存文件系统也可能不支持确保本地文件，因此即使 `IsDone=true`，`FilePath` 仍可能为 null。它不是原始文件解包后的独立磁盘路径。需要原始内容时应始终使用 `GetBytes()`。

这是 YooAsset 3.0.5 升级后的行为契约变化，不是旧 `RawFileHandle.GetRawFilePath()` 语义的完全兼容：接口签名和调用方式保持不变，`GetBytes()` 仍可靠返回原始内容副本，但 `FilePath` 从“原始文件绝对路径”变为“best-effort 底层 bundle 路径”。升级时已检索 Nova 仓库，框架内部没有 `IRawFileHandle.FilePath` 消费方；外部项目如依赖旧路径语义，必须改用 `GetBytes()`。

---

## 关联文档

- [IAssetManager.md](IAssetManager.md)
- [../Definitions/YooAssetRawFileHandleAdapter.md](../Definitions/YooAssetRawFileHandleAdapter.md)
