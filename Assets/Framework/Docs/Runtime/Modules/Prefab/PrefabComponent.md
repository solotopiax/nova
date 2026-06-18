# PrefabComponent

`PrefabComponent` 是 Prefab 实例化系统的场景入口。  
它本身很薄，职责只有：

- 反射创建 `IPrefabManager`
- 把 Inspector 中的 `PrefabManagerConfig` 下发给 Manager
- 对外透传实例化与销毁入口

真正的句柄跟踪和销毁回收都在 `PrefabManager`。

## 什么时候先看这页

优先看这页的场景：

- 你要确认场景里 Prefab 子系统是怎么挂起来的。
- 你要看 `Nova.Prefab.Destroy(go)` 为什么不是普通 `Object.Destroy(go)` 的简单别名。
- 你要判断实例记录和诊断数据是在哪一层维护的。

如果你要看单路径回收和 `PrefabInstanceTag`，继续看 [PrefabManager.md](PrefabManager/PrefabManager.md)。

## 依赖与边界

### 它依赖什么

- `IPrefabManager`
- `PrefabManagerConfig`
- `Util.TypeCreator`

### 它对外暴露什么

- `InstantiateSync(...)`
- `InstantiateAsync(...)`
- `Destroy(...)`
- `RecordedInstanceCount`
- `RecordedInstances`

### 它不负责什么

- 不负责底层资源加载
- 不负责句柄释放
- 不负责实例跟踪字典维护

## 核心流程

### Awake：反射创建 Manager

`Awake()` 会：

1. `base.Awake()`
2. `Util.TypeCreator.Create<IPrefabManager>(m_CurPrefabManagerTypeName)`

类型名无效会直接抛异常。

### Start：只做初始化注入

`Start()` 只调用：

- `m_PrefabManager.Initialize(m_PrefabManagerConfig)`

这里不会提前实例化任何 Prefab。

### 运行时 API 只是门面透传

这些 public 方法都直接转发给 `IPrefabManager`：

- `InstantiateSync(location, parent)`
- `InstantiateSync<T>(...)`
- `InstantiateAsync(location, parent, ct)`
- `InstantiateAsync<T>(...)`
- `Destroy(instance)`

诊断属性也一样：

- `RecordedInstanceCount`
- `RecordedInstances`

## 高价值 API 面

### 1. 实例化

- `InstantiateSync(...)`
- `InstantiateAsync(...)`
- 泛型版本 `InstantiateSync<T>() / InstantiateAsync<T>()`

### 2. 销毁

- `Destroy(GameObject instance)`

### 3. 诊断

- `RecordedInstanceCount`
- `RecordedInstances`

## 风险点 / 易错点

- `PrefabComponent` 只是门面；如果要排查句柄泄漏、实例记录不一致、销毁后未释放，应该直接看 `PrefabManager`。
- `Destroy(instance)` 的真实意义不是“销毁 GameObject”本身，而是进入统一的句柄回收路径。
- `RecordedInstances` 是运行时诊断视图，不是持久化登记表。
- `OnDestroy()` 这里只是清空 Manager 引用，不是实例句柄真正释放点；真正兜底清理在 `PrefabManager.Shutdown()`。

## 继续阅读

关键源码：

- [PrefabComponent.cs](../../../../Scripts/Runtime/Modules/Prefab/PrefabComponent.cs)
- [PrefabComponent.Visitors.cs](../../../../Scripts/Runtime/Modules/Prefab/PrefabComponent.Visitors.cs)

相关文档：

- [PrefabManager.md](PrefabManager/PrefabManager.md)
- [IPrefabManager.md](PrefabManager/IPrefabManager.md)
- [PrefabManagerConfig.md](PrefabManager/PrefabManagerConfig.md)
- [PrefabInstanceTag.md](Definitions/PrefabInstanceTag.md)
