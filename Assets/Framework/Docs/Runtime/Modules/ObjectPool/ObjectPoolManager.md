# ObjectPoolManager

`ObjectPoolManager` 是对象池系统的真实管理核心。  
它管理的不是“一个池”，而是一组以 `Type + Name` 为键的对象池，并负责：

- 创建 / 查询 / 销毁池
- 按优先级轮询每个池
- 统一触发全局释放

## 什么时候先看这页

优先看这页的场景：

- 你要排查为什么某个池重复创建失败。
- 你要看 `SingleGetting` 和 `MultiGetting` 的真实差异。
- 你要确认 `Release()`、`ReleaseAllUnused()` 和自动释放到底怎么触发。
- 你要排查为什么某个对象明明在池里却取不到。

## 依赖与边界

### 它依赖什么

- `ObjectPoolBase`
- `ObjectPool<T>`
- `ObjectBase`
- `ObjectPoolConfig`
- `TypeNamePair`

### 它对外负责什么

- 管理所有已创建对象池
- 统一提供类型 / 名称维度的查询
- 统一创建单取池与多取池
- 统一销毁池
- 统一触发池释放

### 它不负责什么

- 不负责某个业务对象的 `Release(bool isShutdown)` 实现
- 不负责具体对象被取出后的业务使用逻辑
- 不负责纯数据对象的 `ReferencePool`

## 核心流程

### 1. 池键是 `Type + Name`

当前 Manager 内部用 `TypeNamePair` 作为键。  
这意味着：

- 同一个类型、同一个名称，只能有一个池
- 同一个类型可以有多个不同名称的池
- 不同类型就算名称相同，也不是同一个池

重复创建时会直接抛 `InvalidOperationException`。

### 2. 创建分为两种模式

#### SingleGetting

- `CreateSingleGettingObjectPool*`
- `allowMultiGet = false`

语义：

- 对象被 `Get()` 后，在 `Put()` 前不能再次被取出

#### MultiGetting

- `CreateMultiGettingObjectPool*`
- `allowMultiGet = true`

语义：

- 同一个对象在使用中仍然可以再次被 `Get()`

这两个模式的差异不是“优化策略”，而是引用语义差异。

### 3. Manager 的 `Update()` 只是把 Tick 分发给每个池

`ObjectPoolManager.Update()` 本身不做复杂算法。  
它只是遍历 `m_ObjectPools`，逐个调用各池的 `Update()`。

真正的自动释放发生在每个 `ObjectPool<T>` 内部。

### 4. 全局释放会先按池优先级排序

`Release()` 和 `ReleaseAllUnused()` 都会先：

1. `GetAllObjectPools(true, m_CachedAllObjectPools)`
2. 按池 `Priority` 排序
3. 依次调用各池的释放逻辑

所以先后顺序不只是“枚举顺序”，而是明确受池优先级控制。

### 5. 单个池内部的自动释放语义

每个 `ObjectPool<T>` 内部：

- `Update()` 用 `Time.unscaledDeltaTime` 推进 `m_AutoReleaseTimeCounter`
- 到达 `AutoReleaseInterval` 后触发 `Release()`
- `Put()` 后若超出容量，也可能立即触发 `Release()`
- 修改 `Capacity` / `ExpireTime` 也可能触发释放

可进入释放候选的对象必须同时满足：

- 不在使用中
- 未锁定
- `CustomCanReleaseFlag == true`

默认筛选策略是：

1. 先释放已过期对象
2. 再从剩余对象里按“低优先级、最久未使用”优先释放

## 高价值 API 面

### 1. 查询

- `HasObjectPool*`
- `GetObjectPool*`
- `GetAllObjectPools(...)`

### 2. 创建

- `CreateSingleGettingObjectPool*`
- `CreateMultiGettingObjectPool*`

### 3. 销毁与释放

- `DestroyObjectPool*`
- `Release()`
- `ReleaseAllUnused()`

## 关键状态

- `m_ObjectPools`：所有池的真实容器
- `m_CachedAllObjectPools`：避免频繁分配的查询缓存
- `m_ObjectPoolComparer`：基于池优先级排序

## 风险点 / 易错点

- 池的唯一性是 `Type + Name`，不是只有 `Type`，也不是只有 `Name`。
- `SingleGetting` 与 `MultiGetting` 是语义差异，不是“性能配置”。
- Manager 的 `Release()` 不是直接销毁所有对象，而是触发每个池按自身规则释放当前可释放对象。
- 某个对象能否被释放，最终不止看 `Locked`，还要看是否在用、以及 `CustomCanReleaseFlag`。
- 反射创建泛型池的 `CreateMulti/SingleGettingObjectPool(Type, ...)` 在 IL2CPP 下需要相关泛型类型可被 AOT 支持。

## 继续阅读

关键源码：

- [ObjectPoolManager.cs](../../../../Scripts/Runtime/Modules/ObjectPool/Managers/Implements/ObjectPoolManager.cs)
- [ObjectPoolManager.Methods.cs](../../../../Scripts/Runtime/Modules/ObjectPool/Managers/Implements/ObjectPoolManager.Methods.cs)
- [ObjectPoolManager.Visitors.cs](../../../../Scripts/Runtime/Modules/ObjectPool/Managers/Implements/ObjectPoolManager.Visitors.cs)
- [ObjectPool.cs](../../../../Scripts/Runtime/Modules/ObjectPool/Managers/Implements/ObjectPools/ObjectPool.cs)

相关文档：

- [IObjectPoolManager.md](IObjectPoolManager.md)
- [ObjectPoolComponent.md](ObjectPoolComponent.md)
- [IObjectPool.md](IObjectPool.md)
- [ObjectBase.md](ObjectBase.md)
- [ObjectPoolConfig.md](ObjectPoolConfig.md)
- [ReferencePool.md](../../Core/Reference/ReferencePool.md)
