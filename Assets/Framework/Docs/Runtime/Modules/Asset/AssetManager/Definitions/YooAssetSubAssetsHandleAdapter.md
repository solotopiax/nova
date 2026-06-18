# YooAssetSubAssetsHandleAdapter\<T\>

**类签名**：`internal sealed class YooAssetSubAssetsHandleAdapter<T> : ISubAssetsHandle<T>, IReference where T : UnityEngine.Object`
**命名空间**：`NovaFramework.Runtime`

YooAsset.SubAssetsHandle 到 ISubAssetsHandle 的内部适配器，通过 ReferencePool 复用减少 GC 压力。

---

## 文件

| 文件 | 类 | 说明 |
|------|-----|------|
| `Managers/AssetManager/Definitions/YooAssetSubAssetsHandleAdapter.cs` | `YooAssetSubAssetsHandleAdapter<T>` | 适配器实现 + IReference 实现 |

---

## 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `m_Inner` | `SubAssetsHandle` | `null` | 被包装的 YooAsset 原生子资源句柄 |
| `m_Assets` | `T[]` | `null` | 缓存的强类型子资源数组（首次访问 Assets 时构建） |

---

## 完整公开 API

```csharp
// ISubAssetsHandle<T> 实现
bool IsValid { get; }     // m_Inner != null && m_Inner.IsValid
bool IsDone { get; }      // m_Inner != null && m_Inner.IsDone
T[] Assets { get; }       // 缓存的强类型数组（IsDone 为 false 时为 null）

void Release()            // m_Inner?.Release() + ReferencePool.Put(this)

// IReference 实现
void IReference.Clear()   // m_Inner = null; m_Assets = null（归池时调用）

// internal
internal void Bind(SubAssetsHandle inner)  // 绑定原生句柄，m_Assets = null 重置缓存
```

---

## 生命周期

```
AssetManager.LoadSubsSync<T> / LoadSubsAsync<T>
  └── ReferencePool.Get<YooAssetSubAssetsHandleAdapter<T>>()
        └── adapter.Bind(nativeHandle)

调用方持有 ISubAssetsHandle<T>，整批使用完后 Release()
  └── m_Inner?.Release()      // 减 YooAsset 引用计数（整批一次）
  └── ReferencePool.Put(this) // 适配器归池，m_Assets 清空
```

**整批同生共死语义**：Release() 对整批子资源计数归零，不支持对单个元素局部释放。

---

## 使用示例

```csharp
// 外部通过 ISubAssetsHandle<T> 接口使用，无需感知 YooAssetSubAssetsHandleAdapter
ISubAssetsHandle<Sprite> handle = await m_AssetManager.LoadSubsAsync<Sprite>(location);
Sprite[] sprites = handle.Assets;
// 整批使用完毕后一次 Release
handle.Release();
```

---

## 关联文档

- [ISubAssetsHandle.md](../Interfaces/ISubAssetsHandle.md)
- [IAssetManager.md](../Interfaces/IAssetManager.md)
