# ObjectPoolComponent

`ObjectPoolComponent` 是对象池系统的场景入口，也是 `Nova.ObjectPool` 对应的门面。  
它本身不实现池逻辑，主要负责：

- 反射创建 `IObjectPoolManager`
- 在 `Start()` 里初始化 Manager
- 对外透传对象池创建、查询、销毁、释放入口

## 什么时候先看这页

优先看这页的场景：

- 你要确认对象池系统在场景里怎么挂起来。
- 你要看为什么其他模块通常在 `Start()` 才创建自己的池。
- 你要区分 `Release()` 和 `ReleaseAllUnused()` 的职责。

如果你要看池键规则、单取 / 多取模式、自动释放算法，继续看 [ObjectPoolManager.md](ObjectPoolManager.md)。

## 依赖与边界

### 它依赖什么

- `IObjectPoolManager`
- `ObjectPoolManagerConfig`
- `Util.TypeCreator`

### 它对外暴露什么

- `CreateSingleGettingObjectPool*`
- `CreateMultiGettingObjectPool*`
- `GetObjectPool*`
- `DestroyObjectPool*`
- `Release()`
- `ReleaseAllUnused()`

### 它不负责什么

- 不负责单个池的具体释放策略
- 不负责对象引用计数
- 不负责对象真正释放实现

## 核心流程

### Awake：反射创建 Manager

`Awake()` 会：

1. `base.Awake()`
2. `Util.TypeCreator.Create<IObjectPoolManager>(m_CurManagerTypeName)`

类型名无效会直接抛异常。

### Start：初始化 Manager

`Start()` 当前只传入一个空的 `ObjectPoolManagerConfig`。  
也就是说，Manager 本身几乎没有场景侧配置负担，真正重要的是每个具体池自己的 `ObjectPoolConfig`。

### 运行时 API 只是透传

组件层暴露了很多查询 / 创建 / 销毁重载，但它们都只是把调用转发给 `m_ObjectPoolManager`。

真正的关键语义在 Manager 层：

- 池的键怎么组成
- 单取和多取怎么区分
- 自动释放怎么触发

## 高价值 API 面

### 1. 创建

- `CreateSingleGettingObjectPool<T>(config)`
- `CreateMultiGettingObjectPool<T>(config)`

### 2. 查询

- `HasObjectPool*`
- `GetObjectPool*`
- `GetAllObjectPools(...)`

### 3. 销毁与释放

- `DestroyObjectPool*`
- `Release()`
- `ReleaseAllUnused()`

## 风险点 / 易错点

- 组件层不是对象池实现本体；排查释放顺序、重复创建、键冲突时必须直接看 `ObjectPoolManager`。
- `Release()` 只释放“当前可释放对象”，不是强制清空所有对象。
- `ReleaseAllUnused()` 才是“把所有未使用对象都释放掉”。
- `OnDestroy()` 这里只是清空 Manager 引用，不是对象池真正 `Shutdown()` 的执行点。

## 继续阅读

关键源码：

- [ObjectPoolComponent.cs](../../../../Scripts/Runtime/Modules/ObjectPool/ObjectPoolComponent.cs)
- [ObjectPoolComponent.Methods.cs](../../../../Scripts/Runtime/Modules/ObjectPool/ObjectPoolComponent.Methods.cs)
- [ObjectPoolComponent.Visitors.cs](../../../../Scripts/Runtime/Modules/ObjectPool/ObjectPoolComponent.Visitors.cs)

相关文档：

- [ObjectPoolManager.md](ObjectPoolManager.md)
- [IObjectPoolManager.md](IObjectPoolManager.md)
- [ObjectPoolConfig.md](ObjectPoolConfig.md)
- [IObjectPool.md](IObjectPool.md)
