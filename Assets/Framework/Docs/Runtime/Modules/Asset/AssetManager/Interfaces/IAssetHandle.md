# IAssetHandle / IAssetHandle\<T\>

**类签名**：`public interface IAssetHandle` / `public interface IAssetHandle<T> : IAssetHandle where T : UnityEngine.Object`
**命名空间**：`NovaFramework.Runtime`

资源句柄中性接口，不耦合任何第三方资源框架，供 PrefabManager 等需要持有引用计数的调用方使用。

---

## 文件

| 文件 | 类 | 说明 |
|------|-----|------|
| `Managers/AssetManager/Interfaces/IAssetHandle.cs` | `IAssetHandle`, `IAssetHandle<T>` | 非泛型基接口 + 强类型泛型接口 |

---

## 完整公开 API

```csharp
// IAssetHandle（非泛型基接口）
bool IsValid { get; }         // 句柄是否仍有效（未释放）
bool IsDone { get; }          // 异步加载是否完成
UnityEngine.Object AssetObject { get; }  // 已加载资源（IsDone 为 false 时为 null）
void Release();               // 释放句柄（引用计数 -1）

// IAssetHandle<T>（强类型，继承 IAssetHandle）
T Asset { get; }              // 强类型资源对象（IsDone 为 false 时为 null）
```

---

## 使用示例

```csharp
// 短期使用（try/finally 保证 Release）
IAssetHandle<TextAsset> handle = await m_AssetManager.LoadAsync<TextAsset>(location);
try
{
    ProcessData(handle.Asset);
}
finally
{
    handle.Release();
}

// 长期持有（字段持有，生命周期末 Release）
private IAssetHandle<ConfigRuntimeSO> m_ConfigHandle;

var handle = await m_AssetManager.LoadAsync<ConfigRuntimeSO>(location);
m_ConfigHandle = handle;
m_Runtime = handle.Asset;

// Shutdown 时
m_ConfigHandle?.Release();
m_ConfigHandle = null;
```

---

## 常见误区

| 误区 | 正确做法 |
|------|---------|
| 忽略返回值只用 `.Asset` | 必须持有 Handle 并在适当时机 Release，否则引用计数永不归零 |
| IsDone 为 false 时访问 Asset | 同步加载（LoadSync）IsDone 立即为 true；异步加载需等待 |
| Handle 已 Release 后继续访问 Asset | Release 后 IsValid 变 false，AssetObject / Asset 返回 null |

---

## 关联文档

- [IAssetManager.md](IAssetManager.md)
- [YooAssetHandleAdapter.md](../Definitions/YooAssetHandleAdapter.md)

## 内置适配器的租约

Asset、AllAssets、SubAssets 和 Scene 适配器由 ReferencePool 复用。IsDone 只表示结束，IsValid 只表示底层仍有效，均不等价资源加载成功；接口不提供 Succeeded。普通资源 Release、场景 UnloadAsync 后必须丢弃引用，不能再读取属性或重复释放：实例可能已经绑定另一笔操作，旧引用可能操作新租约。

普通/批量 Release 不设幂等标记，先底层 Release 再归池；底层异常会阻止归池。IReference.Clear 只清字段，不执行 Release/Unload，不能手动归池代替领域释放。Bind 不会释放原句柄，只供刚取得的适配器绑定新所有权。Raw 的当前租约释放标记是特例，详见对应文档。
