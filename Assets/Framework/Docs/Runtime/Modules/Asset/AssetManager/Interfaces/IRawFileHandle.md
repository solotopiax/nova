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
string FilePath { get; }  // 原始文件在本地磁盘的绝对路径（IsDone 为 false 时为 null 或空字符串）
byte[] GetBytes();        // 读取文件全部字节（IsDone 为 false 时返回 null）；每次调用均执行磁盘 IO，建议缓存结果
void Release();           // 释放句柄（引用计数 -1）
```

---

## 使用示例

```csharp
// 加载 DLL 字节（HybridCLR 路径）
IRawFileHandle handle = await Nova.Asset.LoadRawAsync("dlls/Game.Runtime", ct);
try
{
    byte[] dllBytes = handle.GetBytes();   // 建议只调用一次并缓存
    Assembly.Load(dllBytes);
}
finally
{
    handle.Release();
}
```

---

## 关联文档

- [IAssetManager.md](IAssetManager.md)
- [../Definitions/YooAssetRawFileHandleAdapter.md](../Definitions/YooAssetRawFileHandleAdapter.md)
