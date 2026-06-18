# IAllAssetsHandle / IAllAssetsHandle\<T\>

**类签名**：`public interface IAllAssetsHandle` / `public interface IAllAssetsHandle<T> : IAllAssetsHandle where T : UnityEngine.Object`
**命名空间**：`NovaFramework.Runtime`

全资源批量句柄中性接口，不耦合任何第三方资源框架；整批资源共用同一引用计数，整批同生共死。

---

## 文件

| 文件 | 类 | 说明 |
|------|-----|------|
| `Managers/AssetManager/Interfaces/IAllAssetsHandle.cs` | `IAllAssetsHandle`, `IAllAssetsHandle<T>` | 非泛型基接口 + 强类型泛型接口 |

---

## 完整公开 API

```csharp
// IAllAssetsHandle（非泛型基接口）
bool IsValid { get; }     // 句柄是否仍有效（未释放）
bool IsDone { get; }      // 异步加载是否完成
void Release();           // 释放整批句柄（引用计数 -1），整批同生共死

// IAllAssetsHandle<T>（强类型，继承 IAllAssetsHandle）
T[] Assets { get; }       // 强类型资源数组（IsDone 为 false 时为 null）
```

---

## 使用示例

```csharp
// 加载某目录下所有 Texture2D（共用一个句柄）
IAllAssetsHandle<Texture2D> handle = await Nova.Asset.LoadAllAsync<Texture2D>("textures/icons", ct);
try
{
    Texture2D[] textures = handle.Assets;
    // 使用 textures...
}
finally
{
    handle.Release();   // 整批释放，不可对单个 Texture2D 单独 Release
}
```

---

## 常见误区

| 误区 | 正确做法 |
|------|---------|
| 对单个资源单独 Release | 整批同生共死，只能整批 Release |
| IsDone 为 false 时访问 Assets | 异步加载时需等待 IsDone 为 true 或等待 UniTask 完成 |

---

## 关联文档

- [IAssetManager.md](IAssetManager.md)
- [IAssetHandle.md](IAssetHandle.md)
- [../Definitions/YooAssetAllAssetsHandleAdapter.md](../Definitions/YooAssetAllAssetsHandleAdapter.md)
