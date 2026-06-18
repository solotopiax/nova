# YooAssetRawFileHandleAdapter

**类签名**：`internal sealed class YooAssetRawFileHandleAdapter : IRawFileHandle, IReference`
**命名空间**：`NovaFramework.Runtime`

YooAsset.RawFileHandle 到 IRawFileHandle 的内部适配器，通过 ReferencePool 复用减少 GC 压力。

---

## 文件

| 文件 | 类 | 说明 |
|------|-----|------|
| `Managers/AssetManager/Definitions/YooAssetRawFileHandleAdapter.cs` | `YooAssetRawFileHandleAdapter` | 适配器实现 + IReference 实现 |

---

## 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `m_Inner` | `RawFileHandle` | `null` | 被包装的 YooAsset 原生原始文件句柄 |

---

## 完整公开 API

```csharp
// IRawFileHandle 实现
bool IsValid { get; }         // m_Inner != null && m_Inner.IsValid
bool IsDone { get; }          // m_Inner != null && m_Inner.IsDone
string FilePath { get; }      // IsDone 为 false 时返回 null；调用 m_Inner.GetRawFilePath()
byte[] GetBytes()             // 每次调用执行磁盘 IO（File.ReadAllBytes(FilePath)）；IsDone 为 false 时返回 null

void Release()                // m_Inner?.Release() + ReferencePool.Put(this)

// IReference 实现
void IReference.Clear()       // m_Inner = null（归池时调用）

// internal
internal void Bind(RawFileHandle inner)  // 绑定原生句柄
```

---

## 注意事项

| 事项 | 说明 |
|------|------|
| GetBytes() 每次触发磁盘 IO | 内部是 `File.ReadAllBytes(FilePath)`，无缓存。调用方如需多次访问文件内容，应自行缓存 byte[]，用完后 Release Handle |
| FilePath 为本地绝对路径 | 路径由 YooAsset 下载/缓存后写入磁盘，正式包与开发模式路径不同，勿硬编码依赖 |

---

## 生命周期

```
AssetManager.LoadRawSync / LoadRawAsync
  └── ReferencePool.Get<YooAssetRawFileHandleAdapter>()
        └── adapter.Bind(nativeHandle)

调用方持有 IRawFileHandle，使用完后 Release()
  └── m_Inner?.Release()      // 减 YooAsset 引用计数
  └── ReferencePool.Put(this) // 适配器归池
```

---

## 使用示例

```csharp
IRawFileHandle handle = await m_AssetManager.LoadRawAsync(location);
// 只需一次读取，自行缓存
byte[] bytes = handle.GetBytes();
handle.Release();  // Release 后不再访问 bytes 引用的内存（bytes 本身安全，Handle 已释放磁盘路径引用计数）
```

---

## 关联文档

- [IRawFileHandle.md](../Interfaces/IRawFileHandle.md)
- [IAssetManager.md](../Interfaces/IAssetManager.md)
