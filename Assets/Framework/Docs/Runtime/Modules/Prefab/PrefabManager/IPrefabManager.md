# IPrefabManager

`IPrefabManager` 定义的是 Prefab 实例管理契约。  
调用方真正可依赖的不是“有几个重载”，而是：

- 实例化后是否会进入统一句柄管理
- 销毁是否走统一回收链
- 是否提供实例诊断视图

## 契约定位

它覆盖三层能力：

- 初始化：`Initialize(config)`
- 实例化 / 销毁：`Instantiate* / Destroy`
- 诊断：`RecordedInstanceCount / RecordedInstances`

直接依赖它的通常是：

- `PrefabComponent`
- `UIManager`
- 任何需要通过资源系统实例化 Prefab 的运行时代码

## 调用方可依赖的语义

### 1. 实例化返回的是实例，不是资源句柄

`IPrefabManager` 对上层暴露的是 `GameObject` 或组件本身。  
句柄管理被下沉到实现内部。

### 2. 销毁的语义是“进入统一回收链”

`Destroy(instance)` 不只是销毁 GameObject。  
更重要的是它对应的实例最终会从内部跟踪表移除，并释放句柄。

### 3. 泛型实例化只是附加的组件获取语义

- `InstantiateSync<T>()`
- `InstantiateAsync<T>()`

它们不改变句柄语义，只是在实例化后额外做一次 `GetComponent<T>()`。

### 4. 诊断能力是只读视图

- `RecordedInstanceCount`
- `RecordedInstances`

它们用于观察当前被跟踪的实例，不是业务层容器本体。

## 最小 API 面

- `Initialize(config)`
- `InstantiateAsync(location, parent, ct)`
- `InstantiateSync(location, parent)`
- `Destroy(instance)`
- `RecordedInstanceCount`

## 变更影响面

如果这里的契约变化，会直接影响：

- [PrefabComponent.md](../PrefabComponent.md)
- [PrefabManager.md](PrefabManager.md)
- [UIManager.md](../../UI/UIManager/UIManager.md)

尤其高风险的是：

- 实例是否继续绑定独立 Handle
- 销毁是否继续汇聚到单路径回收
- 泛型实例化缺组件时是报错返回还是抛异常

## 相关实现

关键源码：

- [IPrefabManager.cs](../../../../../Scripts/Runtime/Modules/Prefab/Managers/PrefabManager/Interfaces/IPrefabManager.cs)

相关文档：

- [PrefabManager.md](PrefabManager.md)
- [PrefabComponent.md](../PrefabComponent.md)
- [PrefabInstanceTag.md](../Definitions/PrefabInstanceTag.md)
