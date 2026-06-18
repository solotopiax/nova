# IUIManager

`IUIManager` 定义的是 UI 运行时契约，不是实现细节目录。  
调用方真正应该依赖的是这些语义是否稳定，而不是接口里一共有多少个重载。

## 契约定位

它把 UI 能力拆成了两层：

- 注册表层：加载 `IUIViewRow` 数据，让泛型打开可用
- 实例层：分组、打开、关闭、查询、回收、安全区

直接依赖它的通常是：

- `UIComponent`
- `Nova.UI` 的外观层
- 需要统一打开 / 关闭 / 查询 UI 的运行时代码

## 调用方可依赖的语义

### 1. 初始化被拆成两个阶段

- `Initialize(UIManagerConfig config)`：只接收配置和依赖
- `CreateInstancePool()`：等对象池管理器就绪后再接入对象池

调用方不应该假设 `Initialize` 之后对象池已经可用。

### 2. 注册表加载与实例打开是两回事

- `LoadAsync()` / `LoadSync()` 负责注册表
- `OpenUIView*()` 负责实例

调用方可以依赖：

- 泛型打开依赖注册表
- 显式 `assetLocation + uiGroupName` 打开不依赖注册表

### 3. 打开视图的最小语义

- 会返回一个 `serialID`
- 分组必须存在
- 同步 / 异步只是实例获取方式不同，不改变分组和生命周期语义
- `pauseCoveredUIView` 决定遮挡下层时是否传播暂停
- `inObjectPools` 决定关闭后回对象池还是直接销毁

### 4. 关闭视图分成“已加载”和“加载中”两类

- `CloseUIView(...)` 用于已加载实例
- `CloseAllLoadingUIViews()` 用于取消正在异步加载的实例

调用方不应把这两类关闭语义混为一谈。

### 5. 查询语义

- `GetUIGroup(...)` / `HasUIGroup(...)` 针对分组
- `GetUIView(...)` / `GetUIViews(...)` / `HasUIView(...)` 针对已加载视图
- `IsLoadingUIView(...)` 针对异步加载中的视图
- `IsValidUIView(...)` 判断传入实例当前是否仍受管理

### 6. 运行时调优语义

- `InstanceAutoReleaseInterval / Capacity / ExpireTime / Priority` 是实例池调优项
- `DestroyMaxNumPerFrame` 是关闭后统一回收的节流项
- `SetUIViewTargetLocked / SetUIViewTargetPriority` 作用于实例对象池目标
- `GetDeviceSafeArea()` 返回设备安全区矩形

## 最小 API 面

- 初始化：`Initialize(...)` / `CreateInstancePool()`
- 注册表：`LoadSync()` / `LoadAsync()`
- 打开：`OpenUIViewSync<T>()` / `OpenUIViewAsync<T>()`
- 关闭：`CloseUIView(...)` / `CloseAllLoadedUIViews(...)` / `CloseAllLoadingUIViews()`
- 查询：`GetUIView(...)` / `HasUIView(...)` / `IsLoadingUIView(...)`
- 分组：`AddUIGroup(...)` / `GetUIGroup(...)`

## 变更影响面

如果这里的契约变化，会直接影响：

- [UIComponent.md](../../UIComponent.md)
- [UIManager.md](../UIManager.md)
- 所有依赖 `Nova.UI` 或 `UIComponent` 的业务代码

尤其高风险的是：

- 泛型打开是否继续依赖注册表
- `serialID` 语义是否变化
- “关闭加载中视图”与“关闭已加载视图”的边界是否变化
- 分组必须先注册的前置条件是否变化
- 对象池参数是否还是运行时可调

## 相关实现

关键源码：

- [IUIManager.cs](../../../../../../Scripts/Runtime/Modules/UI/Managers/UIManager/Interfaces/IUIManager.cs)

相关文档：

- [UIManager.md](../UIManager.md)
- [UIComponent.md](../../UIComponent.md)
- [UIManagerConfig.md](../Definitions/UIManagerConfig.md)
- [UIView.md](../../Definitions/UIView.md)
- [UIGroupHelper.md](../../UIGroupHelper/UIGroupHelper.md)
