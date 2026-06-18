# YooAssetSceneHandleAdapter

**类签名**：`internal sealed class YooAssetSceneHandleAdapter : ISceneHandle, IReference`
**命名空间**：`NovaFramework.Runtime`

YooAsset.SceneHandle 到 ISceneHandle 的内部适配器，通过 ReferencePool 复用减少 GC 压力；卸载场景通过 `UnloadAsync()` 完成，之后自动归池。

---

## 文件

| 文件 | 类 | 说明 |
|------|-----|------|
| `Managers/AssetManager/Definitions/YooAssetSceneHandleAdapter.cs` | `YooAssetSceneHandleAdapter` | 适配器实现 + IReference 实现 |

---

## 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `m_Inner` | `YooAsset.SceneHandle` | `null` | 被包装的 YooAsset 原生场景句柄 |

---

## 完整公开 API

```csharp
// ISceneHandle 实现
bool IsValid { get; }         // m_Inner != null && m_Inner.IsValid
bool IsDone { get; }          // m_Inner != null && m_Inner.IsDone

async UniTask UnloadAsync()   // 调用 m_Inner.UnloadSceneAsync()，WaitUntil(op.IsDone)，之后 ReferencePool.Put(this)

// IReference 实现
void IReference.Clear()       // m_Inner = null（归池时调用）

// internal
internal void Bind(YooAsset.SceneHandle inner)  // 绑定原生句柄
```

---

## 注意事项

| 事项 | 说明 |
|------|------|
| 无 Release() 方法 | ISceneHandle 卸载场景**唯一入口**是 `await handle.UnloadAsync()`；调用后适配器自动归池 |
| UnloadAsync 是异步 | 内部等待 `op.IsDone` 后才 `ReferencePool.Put`；在 UnloadAsync 返回前 Handle 仍有效 |
| 卸载后勿访问 Handle | UnloadAsync 完成后 IsValid 变 false，不应再访问 Handle 的任何成员 |

---

## 生命周期

```
AssetManager.LoadSceneSync / LoadSceneAsync
  └── ReferencePool.Get<YooAssetSceneHandleAdapter>()
        └── adapter.Bind(nativeHandle)

调用方持有 ISceneHandle，卸载场景时：
  await handle.UnloadAsync()
    └── m_Inner.UnloadSceneAsync()          // 触发 YooAsset 场景卸载
    └── UniTask.WaitUntil(() => op.IsDone)  // 等待卸载完成
    └── ReferencePool.Put(this)             // 适配器归池
```

---

## 使用示例

```csharp
// 加载场景
ISceneHandle sceneHandle = await Nova.Asset.LoadSceneAsync(location);

// ... 使用场景 ...

// 卸载场景（异步，无同步等价）
await sceneHandle.UnloadAsync();
sceneHandle = null;
```

---

## 关联文档

- [ISceneHandle.md](../Interfaces/ISceneHandle.md)
- [IAssetManager.md](../Interfaces/IAssetManager.md)
