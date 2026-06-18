# PrefabManager

`PrefabManager` 是 Prefab 子系统的真实实现。  
它解决的核心问题不是“把一个 Prefab Instantiate 出来”，而是：

- 每个实例如何绑定独立资源句柄
- 无论从哪条销毁路径离开，句柄都只释放一次

## 什么时候先看这页

优先看这页的场景：

- 你要排查 Prefab 实例销毁后句柄为什么没释放。
- 你要确认 `Object.Destroy(go)` 和 `Nova.Prefab.Destroy(go)` 的关系。
- 你要看为什么这里需要 `PrefabInstanceTag`。
- 你要判断 `RecordedInstances` 到底记录了什么。

## 依赖与边界

### 它依赖什么

- `IAssetManager`
- `PrefabManagerConfig`
- `PrefabInstanceTag`
- `PrefabRecordedInstance`

### 它对外负责什么

- 同步 / 异步实例化 Prefab
- 为每个实例持有一份独立 `IAssetHandle<GameObject>`
- 跟踪实例到句柄的映射
- 统一销毁回收路径

### 它不负责什么

- 不负责资源系统启动
- 不负责下载或 Manifest
- 不负责业务层对象池复用

## 核心流程

### 1. Initialize：接入 `IAssetManager`

`Initialize(config)` 会：

1. 校验 `config`
2. 记录 `m_Config`
3. 从 `FrameworkManagersGroup` 获取 `IAssetManager`

如果拿不到 `IAssetManager`，这里会直接失败。  
这说明 Prefab 子系统明确依赖资源子系统已注册。

### 2. 每次实例化都会申请独立 Handle

`InstantiateSync/Async(location, parent)` 的真实流程：

1. 通过 `IAssetManager.LoadSync/LoadAsync<GameObject>(location)` 取句柄
2. 从 `handle.Asset` 实例化出 `GameObject`
3. 调 `RecordInstance(go, handle, location)`

它不是共享一个 Prefab 句柄再无限 Instantiate，而是每次实例都绑定一份独立 Handle。

### 3. RecordInstance：把回收钩子钉到实例上

`RecordInstance(...)` 会：

1. 给实例挂 `PrefabInstanceTag`
2. 把 `handle` 填进去
3. 把 `OnDestroyed` 回调指向 `OnInstanceDestroyed`
4. 写入 `m_InstanceToHandle`
5. 写入 `m_RecordedInstances`

这一步建立了“实例销毁 -> 句柄释放”的单路径回收链。

### 4. Destroy：只负责触发 Unity 销毁

`Destroy(instance)` 当前实现只有一件事：

- `Object.Destroy(instance)`

句柄释放不在这里直接做，而是在 `PrefabInstanceTag.OnDestroy()` 触发后，统一走 `OnInstanceDestroyed(tag)`。

### 5. Shutdown：发布统一兜底释放

如果还有实例没正常走到销毁回调，`Shutdown()` 会遍历 `m_InstanceToHandle`，逐个 `Release()`，再清空记录。

## 高价值 API 面

### 1. 实例化

- `InstantiateSync(location, parent)`
- `InstantiateAsync(location, parent, ct)`
- `InstantiateSync<T>(...)`
- `InstantiateAsync<T>(...)`

### 2. 销毁

- `Destroy(GameObject instance)`

### 3. 诊断

- `RecordedInstanceCount`
- `RecordedInstances`

## 关键状态

- `m_AssetManager`：Prefab 资源句柄来源
- `m_InstanceToHandle`：实例到 Handle 的真实索引
- `m_RecordedInstances`：用于诊断展示的轻量记录

## 风险点 / 易错点

- 这里强依赖 `IAssetManager`；如果 Asset 子系统没先起来，Prefab 子系统不会独立工作。
- `InstantiateSync<T>() / InstantiateAsync<T>()` 组件缺失时只记错误日志并返回 `null`，不是抛异常。
- 句柄释放依赖 `PrefabInstanceTag` 的 `OnDestroy` 单路径；如果你绕开这条链条自己瞎管句柄，很容易双重释放或泄漏。
- `Destroy(go)` 和直接 `Object.Destroy(go)` 最终都会走同一条回收回调；统一用 `Nova.Prefab.Destroy(go)` 只是为了语义更清晰。
- `RecordedInstances` 是诊断视图，不保证用于高频业务逻辑查询是高效的。

## 继续阅读

关键源码：

- [PrefabManager.cs](../../../../../Scripts/Runtime/Modules/Prefab/Managers/PrefabManager/Implements/PrefabManager.cs)
- [PrefabManager.Load.cs](../../../../../Scripts/Runtime/Modules/Prefab/Managers/PrefabManager/Implements/PrefabManager.Load.cs)
- [PrefabManager.Methods.cs](../../../../../Scripts/Runtime/Modules/Prefab/Managers/PrefabManager/Implements/PrefabManager.Methods.cs)

相关文档：

- [IPrefabManager.md](IPrefabManager.md)
- [PrefabComponent.md](../PrefabComponent.md)
- [PrefabInstanceTag.md](../Definitions/PrefabInstanceTag.md)
- [PrefabRecordedInstance.md](../Definitions/PrefabRecordedInstance.md)
