# ObjectPoolManager

`ObjectPoolManager` 是对象池系统的真实管理核心。  
它管理的不是“一个池”，而是一组以 `Type + Name` 为键的对象池，并负责：

- 创建 / 查询 / 销毁池
- 将逐帧轮询分发给每个池
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
2. 按池 `Priority` 升序排序，数值较小的池先处理
3. 依次调用各池的释放逻辑

所以先后顺序不只是“枚举顺序”，而是明确受池优先级控制。

该排序用于这两个全局释放入口及显式 `GetAllObjectPools(sort: true)`；`Update()` 直接遍历字典，不按池优先级排序。池优先级也不替代池内对象的 `ObjectBase.Priority`。

### 5. 单个池内部的自动释放语义

每个 `ObjectPool<T>` 内部：

- `Update()` 用 `Time.unscaledDeltaTime` 推进 `m_AutoReleaseTimeCounter`
- 到达 `AutoReleaseInterval` 后触发 `Release()`
- `Register()` 后超出容量会触发 `Release()`；`Put()` 还要求本次归还对象的引用计数已不大于 0
- `Capacity` / `ExpireTime` setter 接受不同的新值后触发 `Release()`

可进入释放候选的对象必须同时满足：

- 不在使用中
- 未锁定
- `CustomCanReleaseFlag == true`

默认筛选策略是：

1. 先选择全部已过期候选，即使目标数量为 0；这一步可超过目标数量
2. 若还未达到目标数量，再从剩余对象按 `ObjectBase.Priority` 升序、同值时 `LastUseTime` 从旧到新补足

无参 `Release()` 的目标数量为 `Count - Capacity`，负数修正为 0。`ReleaseAllUnused()` 不经过这个默认筛选器，会选择全部当前候选，但仍执行上述三个保护条件；详见 [ObjectPool.md](ObjectPool.md)。

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

## 参数与所有权边界

默认 Initialize(config) 当前为空，接受 null 也不创建默认池；Component Awake 反射创建 Manager、Start 传空配置，OnDestroy 仅断引用。Component API 与 Count 通常直接解引用，无未就绪兜底；日志/下层异常继续传播。Base Priority=2 决定全框架较早 Update、较晚 Shutdown，与池 Priority/对象 Priority 是三个不同层级。

Type 参数是 ObjectBase 包装派生类型，不是 Target 类型；仅检查非空与 IsAssignableFrom，不拒抽象、开放泛型或缺少 AOT，Type 创建在 MakeGenericType/Activator 处仍可能失败。config=null 使用新默认配置，创建只构造空池，不创建 T/Target；各项复制后不热绑定 DTO，数值不经过运行 setter。唯一性检查与 Add 无锁，构造后 Add 失败没有 Shutdown 回滚。

无名查询/销毁只匹配 empty 名，null 池名同样归 empty；精确 Type+Name 键不按 namespace/字符串宽匹配。predicate 必须非 null、按本次字典枚举即时调用，首匹配不保证 Priority 顺序；结果数组为新容器但成员是原池引用，List 重载先 Clear，异常可留下部分结果。共享缓存及字典不保护并发/回调重入，持有查询结果不延长池注册或资源寿命。

DestroyObjectPool 的引用重载只读取 Type/Name，不验证属于本 Manager 或引用同一性；外来同键池可定位并销毁本地池。找到后先 Shutdown、成功才 Remove，异常时键仍可存在而资源已部分释放；不存在返回 false。全局 Release 取 Priority 升序结果，但 Update 与 Shutdown 直接按字典枚举，单池异常中断后续及最后清场，均无跨池事务。

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
