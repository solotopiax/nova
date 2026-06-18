# Bases/Interfaces

基础层公共接口。

| 文件 | 接口 | 说明 |
|------|------|------|
| `ICoroutineRunner.cs` | `ICoroutineRunner` | 协程运行器接口，供非 MonoBehaviour 类借用 MonoBehaviour 启动协程 |
| `IReadOnlyOrderedDictionary.cs` | `IReadOnlyOrderedDictionary<TKey,TValue>` | 只读有序字典接口，由 `NovaOrderedDictionary` 实现 |
