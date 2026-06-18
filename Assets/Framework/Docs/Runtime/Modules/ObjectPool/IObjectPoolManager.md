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
- `ReleaseAllUnused()`：通知每个池强制释放全部未使用对象

接口层并不承诺“立刻清空所有对象”。

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

## 相关实现

关键源码：

- [IObjectPoolManager.cs](../../../../Scripts/Runtime/Modules/ObjectPool/Managers/Interfaces/IObjectPoolManager.cs)

相关文档：

- [ObjectPoolManager.md](ObjectPoolManager.md)
- [ObjectPoolComponent.md](ObjectPoolComponent.md)
- [IObjectPool.md](IObjectPool.md)
- [ObjectPoolConfig.md](ObjectPoolConfig.md)
