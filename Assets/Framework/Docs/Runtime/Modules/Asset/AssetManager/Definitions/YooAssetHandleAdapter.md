# YooAssetHandleAdapter\<T\>

**类签名**：`internal sealed class YooAssetHandleAdapter<T> : IAssetHandle<T>, IReference where T : UnityEngine.Object`
**命名空间**：`NovaFramework.Runtime`

YooAsset.AssetHandle 到 IAssetHandle 的内部适配器，通过 ReferencePool 复用减少 GC 压力。

---

## 文件

| 文件 | 类 | 说明 |
|------|-----|------|
| `Managers/AssetManager/Definitions/YooAssetHandleAdapter.cs` | `YooAssetHandleAdapter<T>` | 适配器实现，IReference 实现 |

---

## 关键字段表

| 字段 | 类型 | 说明 |
|------|------|------|
| `m_Inner` | `AssetHandle` | 被包装的 YooAsset 原生句柄（private） |

---

## 完整公开 API

```csharp
// IAssetHandle<T> 实现
bool IsValid { get; }         // m_Inner != null && m_Inner.IsValid
bool IsDone { get; }          // m_Inner != null && m_Inner.IsDone
UnityEngine.Object AssetObject { get; }  // m_Inner?.AssetObject
T Asset { get; }              // m_Inner?.GetAssetObject<T>()

void Release()                // m_Inner.Release() + ReferencePool.Put(this)

// IReference 实现
void IReference.Clear()       // m_Inner = null（归池时调用）

// internal
internal void Bind(AssetHandle inner)  // 绑定原生句柄
```

---

## 生命周期

```
AssetManager.LoadSync<T> / LoadAsync<T>
  └── ReferencePool.Get<YooAssetHandleAdapter<T>>()
        └── adapter.Bind(nativeHandle)

调用方持有 IAssetHandle<T>，使用完后 Release()
  └── m_Inner.Release()       // 减 YooAsset 引用计数
  └── ReferencePool.Put(this) // 适配器归池
```

---

## 使用示例

```csharp
// 外部通过 IAssetHandle<T> 接口使用，无需感知 YooAssetHandleAdapter
IAssetHandle<GameObject> handle = await m_AssetManager.LoadAsync<GameObject>(location);
// ... 用完后
handle.Release();
```

---

## 关联文档

- [IAssetHandle.md](../Interfaces/IAssetHandle.md)
- [IAssetManager.md](../Interfaces/IAssetManager.md)

## 释放与完成状态

IsDone/IsValid 不表示 Succeeded，Asset/AssetObject 直接读取底层对象，不额外检查完成或成功。Release 无重复保护，先释放 inner 再归池；底层 Release 异常时不在 finally 归池。Clear 只清 inner，Bind 不释放此前 inner。调用方每笔所有权只 Release 一次并丢弃引用，不能在池复用后继续使用旧引用。
