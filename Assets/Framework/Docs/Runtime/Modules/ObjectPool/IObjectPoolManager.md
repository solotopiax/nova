# IObjectPoolManager

`IObjectPoolManager` 定义的是“多池管理契约”。  
调用方应该依赖的是这些语义：

- 如何按类型 / 名称找到池
- 如何区分单取池与多取池
- 如何销毁池
- 如何触发全局释放

## 契约定位

它覆盖四层能力：

- 初始化：`Initialize(...)`
- 查询：`HasObjectPool* / GetObjectPool* / GetAllObjectPools*`
- 创建 / 销毁：`Create* / Destroy*`
- 全局释放：`Release / ReleaseAllUnused`

直接依赖它的通常是：

- `ObjectPoolComponent`
- `UIManager`
- `Sound`、`Prefab` 等需要自建池的运行时模块

## 调用方可依赖的语义

### 1. 池是按 `Type + Name` 管理的

调用方可以依赖：

- 同类型可有多个具名池
- 查询和销毁都可以走类型或类型+名称维度

### 2. 创建分单取与多取两类

- `CreateSingleGettingObjectPool*`：对象在归还前不可再次获取
- `CreateMultiGettingObjectPool*`：对象在使用中仍可被再次获取

### 3. 全局释放与单池释放不同层级

- `Release()`：通知每个池按自己的规则释放当前可释放对象
- `ReleaseAllUnused()`：通知每个池跳过容量与过期筛选，释放全部当前可释放的未使用对象；仍保护锁定对象及 `CustomCanReleaseFlag == false` 的对象

两个全局入口都先按池的 `Priority` 升序遍历；接口层并不承诺“立刻清空所有对象”。

### 4. 查询返回的是池，不是对象

`IObjectPoolManager` 的职责止于“找到哪个池”，对象的 `Register / Get / Put / ReleaseObject` 属于 `IObjectPool<T>`。

## 最小 API 面

- 查询：`HasObjectPool<T>()` / `GetObjectPool<T>()`
- 创建：`CreateSingleGettingObjectPool<T>()`
- 销毁：`DestroyObjectPool<T>()`
- 全局释放：`Release()` / `ReleaseAllUnused()`

## 变更影响面

如果这里的契约变化，会直接影响：

- [ObjectPoolComponent.md](ObjectPoolComponent.md)
- [ObjectPoolManager.md](ObjectPoolManager.md)
- [UIManager.md](../UI/UIManager/UIManager.md)

高风险变化包括：

- 池键是否仍然包含名称维度
- 单取 / 多取的语义是否变化
- `Release()` 与 `ReleaseAllUnused()` 的边界是否变化

## 参数与所有权边界

接口/Base 抽象声明不执行上述默认实现。Initialize 当前空体、Type/Name/predicate 的校验和查询结果生命周期，及 Destroy 按键而非引用同一性等实际边界，见 ObjectPoolManager；外部实现须另核。

## 相关实现

关键源码：

- [IObjectPoolManager.cs](../../../../Scripts/Runtime/Modules/ObjectPool/Managers/Interfaces/IObjectPoolManager.cs)

相关文档：

- [ObjectPoolManager.md](ObjectPoolManager.md)
- [ObjectPoolComponent.md](ObjectPoolComponent.md)
- [IObjectPool.md](IObjectPool.md)
- [ObjectPoolConfig.md](ObjectPoolConfig.md)
