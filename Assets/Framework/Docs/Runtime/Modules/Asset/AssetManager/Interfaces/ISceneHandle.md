# ISceneHandle

**类签名**：`public interface ISceneHandle`
**命名空间**：`NovaFramework.Runtime`

场景句柄中性接口，场景加载完成后持有此句柄；调用 UnloadAsync 卸载场景并归还引用计数。

---

## 文件

| 文件 | 类 | 说明 |
|------|-----|------|
| `Managers/AssetManager/Interfaces/ISceneHandle.cs` | `ISceneHandle` | 接口定义 |

---

## 完整公开 API

```csharp
bool IsValid { get; }            // 句柄是否仍有效（未释放）
bool IsDone { get; }             // 场景异步加载是否完成
UniTask UnloadAsync();           // 异步卸载场景并释放句柄（引用计数 -1）
```

> 场景 Handle 不提供 `Release()` 方法。卸载场景的唯一入口是 `UnloadAsync()`，禁止直接丢弃句柄。

---

## 使用示例

```csharp
// 以叠加模式加载场景，持有句柄
ISceneHandle handle = await Nova.Asset.LoadSceneAsync("scenes/Level1", LoadSceneMode.Additive, ct);

// 场景使用期间持有 handle...

// 切换场景时卸载
await handle.UnloadAsync();
// 卸载完成后句柄已归池，不可再访问
```

---

## 常见误区

| 误区 | 正确做法 |
|------|---------|
| 直接丢弃 ISceneHandle 不 UnloadAsync | 场景引用计数泄漏，场景不会被真正卸载 |
| 多次调用 UnloadAsync | `UnloadAsync()` 完成后句柄会立即归池并失效；再次调用或再次访问都不安全，不应发生 |

---

## 关联文档

- [IAssetManager.md](IAssetManager.md)
- [../Definitions/YooAssetSceneHandleAdapter.md](../Definitions/YooAssetSceneHandleAdapter.md)
