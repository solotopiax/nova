# YooAssetRawFileHandleAdapter

**类签名**：`internal sealed class YooAssetRawFileHandleAdapter : IRawFileHandle, IReference`
**命名空间**：`NovaFramework.Runtime`

YooAsset `AssetHandle` / `RawFileObject` 到 `IRawFileHandle` 的内部适配器，通过 ReferencePool 复用并保持 Nova 公共接口不变。

这里保持的是类型签名与调用方式，不是旧路径语义的完全兼容：`GetBytes()` 继续提供可靠的原始内容副本，`FilePath` 改为 best-effort 底层 bundle 路径并允许为 null。

---

## 文件

| 文件 | 类 | 说明 |
|------|-----|------|
| `Managers/AssetManager/Definitions/YooAssetRawFileHandleAdapter.cs` | `YooAssetRawFileHandleAdapter` | 适配器实现 + IReference 实现 |

---

## 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `m_Inner` | `AssetHandle` | `null` | 持有 RawFileObject 生命周期的 YooAsset 资源句柄 |
| `m_RawFile` | `RawFileObject` | `null` | 提供原始文件字节副本的资源对象 |
| `m_FilePath` | `string` | `null` | `EnsureBundleFileAsync` 返回的底层 bundle 文件路径 |
| `m_IsReleased` | `bool` | `true` | 当前租约释放标记，用于避免重复 Release / 重复归池 |

---

## 完整公开 API

```csharp
// IRawFileHandle 实现
bool IsValid { get; }         // m_Inner != null && m_Inner.IsValid
bool IsDone { get; }          // m_Inner != null && m_Inner.IsDone
string FilePath { get; }      // 仅在有效且完成时尽力返回底层 bundle 文件路径，否则为 null
byte[] GetBytes()             // 仅在有效且完成时调用 RawFileObject.GetBytes() 返回字节副本

void Release()                // 首次调用释放 AssetHandle 并归池；后续重复调用安全返回

// IReference 实现
void IReference.Clear()       // 清空句柄、对象、路径并标记已释放

// internal
internal void Bind(AssetHandle inner, RawFileObject rawFile, string filePath)
```

---

## 注意事项

| 事项 | 说明 |
|------|------|
| GetBytes() 返回副本 | 内部调用 `RawFileObject.GetBytes()`，不依赖 FilePath；调用方用完后仍必须 Release Handle |
| FilePath 是可选 bundle 路径 | 异步加载尽力通过 `EnsureBundleFileAsync(...).Detail.BundleFilePath` 补充；同步加载及不支持 Ensure 的 Web/内存文件系统返回 null |
| Release 幂等 | 首次 Release 释放 AssetHandle 并归池，重复调用不会再次减引用计数或重复归池 |

---

## 生命周期

```
AssetManager.LoadRawSync / LoadRawAsync
  ├── LoadAssetSync/Async<RawFileObject>(location)
  ├── 异步路径基于已加载句柄的 AssetInfo 尽力执行 EnsureBundleFileAsync，失败时路径为 null
  └── adapter.Bind(assetHandle, rawFileObject, bundleFilePath)

调用方持有 IRawFileHandle，使用完后 Release()
  └── m_Inner.Release()       // 减 YooAsset 引用计数
  └── ReferencePool.Put(this) // 适配器归池
```

---

## 使用示例

```csharp
IRawFileHandle handle = await m_AssetManager.LoadRawAsync(location);
byte[] bytes = handle.GetBytes();
handle.Release();  // bytes 是副本，Release 后仍可由调用方继续使用
```

---

## 关联文档

- [IRawFileHandle.md](../Interfaces/IRawFileHandle.md)
- [IAssetManager.md](../Interfaces/IAssetManager.md)
