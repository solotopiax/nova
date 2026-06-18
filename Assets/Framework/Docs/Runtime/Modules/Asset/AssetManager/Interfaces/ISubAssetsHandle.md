# ISubAssetsHandle / ISubAssetsHandle\<T\>

**类签名**：`public interface ISubAssetsHandle` / `public interface ISubAssetsHandle<T> : ISubAssetsHandle where T : UnityEngine.Object`
**命名空间**：`NovaFramework.Runtime`

子资源批量句柄中性接口，不耦合任何第三方资源框架；整批子资源共用同一引用计数，整批同生共死。

---

## 文件

| 文件 | 类 | 说明 |
|------|-----|------|
| `Managers/AssetManager/Interfaces/ISubAssetsHandle.cs` | `ISubAssetsHandle`, `ISubAssetsHandle<T>` | 非泛型基接口 + 强类型泛型接口 |

---

## 完整公开 API

```csharp
// ISubAssetsHandle（非泛型基接口）
bool IsValid { get; }     // 句柄是否仍有效（未释放）
bool IsDone { get; }      // 异步加载是否完成
void Release();           // 释放整批句柄（引用计数 -1），整批同生共死

// ISubAssetsHandle<T>（强类型，继承 ISubAssetsHandle）
T[] Assets { get; }       // 强类型子资源数组（IsDone 为 false 时为 null）
```

---

## 使用示例

```csharp
// 加载 Sprite 图集子资源（所有 Sprite 共用一个句柄）
ISubAssetsHandle<Sprite> handle = await Nova.Asset.LoadSubsAsync<Sprite>("ui/atlas/main", ct);
try
{
    Sprite[] sprites = handle.Assets;
    // 使用 sprites...
}
finally
{
    handle.Release();   // 整批释放，不可对单个 Sprite 单独 Release
}
```

---

## 常见误区

| 误区 | 正确做法 |
|------|---------|
| 对单个子资源单独 Release | 整批同生共死，只能整批 Release，不可局部释放 |
| IsDone 为 false 时访问 Assets | 异步加载时需等待 IsDone 为 true 或等待 UniTask 完成 |

---

## 关联文档

- [IAssetManager.md](IAssetManager.md)
- [IAssetHandle.md](IAssetHandle.md)
- [../Definitions/YooAssetSubAssetsHandleAdapter.md](../Definitions/YooAssetSubAssetsHandleAdapter.md)
