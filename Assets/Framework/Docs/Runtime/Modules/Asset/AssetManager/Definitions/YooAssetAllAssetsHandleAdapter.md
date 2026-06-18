# YooAssetAllAssetsHandleAdapter\<T\>

**类签名**：`internal sealed class YooAssetAllAssetsHandleAdapter<T> : IAllAssetsHandle<T>, IReference where T : UnityEngine.Object`
**命名空间**：`NovaFramework.Runtime`

YooAsset.AllAssetsHandle 到 IAllAssetsHandle 的内部适配器，通过 ReferencePool 复用减少 GC 压力；Assets 属性用两遍循环避免 LINQ 分配。

---

## 文件

| 文件 | 类 | 说明 |
|------|-----|------|
| `Managers/AssetManager/Definitions/YooAssetAllAssetsHandleAdapter.cs` | `YooAssetAllAssetsHandleAdapter<T>` | 适配器实现 + IReference 实现 |

---

## 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `m_Inner` | `AllAssetsHandle` | `null` | 被包装的 YooAsset 原生全资源句柄 |
| `m_Assets` | `T[]` | `null` | 缓存的强类型资源数组（首次访问 Assets 时构建，两遍循环过滤类型） |

---

## 完整公开 API

```csharp
// IAllAssetsHandle<T> 实现
bool IsValid { get; }     // m_Inner != null && m_Inner.IsValid
bool IsDone { get; }      // m_Inner != null && m_Inner.IsDone
T[] Assets { get; }       // 缓存的强类型数组（IsDone 为 false 时为 null）

void Release()            // m_Inner?.Release() + ReferencePool.Put(this)

// IReference 实现
void IReference.Clear()   // m_Inner = null; m_Assets = null（归池时调用）

// internal
internal void Bind(AllAssetsHandle inner)  // 绑定原生句柄，m_Assets = null 重置缓存
```

---

## 关键算法：Assets 属性两遍循环

`AllAssetsHandle.AllAssetObjects` 返回 `IReadOnlyList<UnityEngine.Object>`，包含所有类型资源。为构造强类型 T[] 且避免 LINQ `.OfType<T>().ToArray()` 分配两次：

```
第一遍：遍历计数 T 类型元素个数 count
new T[count]
第二遍：遍历填充数组
```

零 LINQ，零额外 IEnumerable 分配，仅一次数组 new。

---

## 生命周期

```
AssetManager.LoadAllSync<T> / LoadAllAsync<T>
  └── ReferencePool.Get<YooAssetAllAssetsHandleAdapter<T>>()
        └── adapter.Bind(nativeHandle)

调用方持有 IAllAssetsHandle<T>，整批使用完后 Release()
  └── m_Inner?.Release()      // 减 YooAsset 引用计数（整批一次）
  └── ReferencePool.Put(this) // 适配器归池，m_Assets 清空
```

**整批同生共死语义**：Release() 对整批资源计数归零，不支持对单个元素局部释放。

---

## 使用示例

```csharp
// 外部通过 IAllAssetsHandle<T> 接口使用，无需感知 YooAssetAllAssetsHandleAdapter
IAllAssetsHandle<AudioClip> handle = await m_AssetManager.LoadAllAsync<AudioClip>(location);
AudioClip[] clips = handle.Assets;
// 整批使用完毕后一次 Release
handle.Release();
```

---

## 关联文档

- [IAllAssetsHandle.md](../Interfaces/IAllAssetsHandle.md)
- [IAssetManager.md](../Interfaces/IAssetManager.md)
