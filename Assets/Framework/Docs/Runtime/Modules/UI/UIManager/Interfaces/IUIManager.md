# IUIManager

`IUIManager` 定义的是 UI 运行时契约，不是实现细节目录。  
调用方真正应该依赖的是这些语义是否稳定，而不是接口里一共有多少个重载。

## 契约定位

它把 UI 能力拆成了两层：

- 注册表层：加载 `IUIViewRow` 数据，让泛型、名称和资源地址打开可用
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

- 所有打开入口都依赖已加载的注册表
- 泛型按 `typeof(T).Name` 匹配；名称入口按表内 `Name` 匹配；资源地址入口按表内 `AssetLocation` 匹配
- `OpenUIViewSync/Async(string uiViewName, object userData = null)` 始终按 `Name`，只有 `OpenUIViewByAssetSync/Async` 按资源地址；不自动检测或混用两种身份

### 3. 打开视图的最小语义

- 配置匹配成功并创建请求后返回非负 `serialID`；配置缺失或匹配不唯一时返回 `-1`
- 分组必须存在
- 同步 / 异步只是实例获取方式不同，不改变分组和生命周期语义
- 表内 `PauseCoveredUIView` 决定遮挡下层时是否传播暂停
- 表内 `InObjectPools` 决定关闭后的实例是归池复用还是销毁；实际回收仍由后续 `Update()` 处理
- `userData` 只承载业务数据，不覆盖资源地址、分组或两个开关

字符串入口如下，`uiViewName` 是 UI 表某一行的 `Name` 值，不是 Excel 文件名；第二个参数始终是业务数据，即使它也是字符串：

```csharp
int OpenUIViewSync(string uiViewName, object userData = null);
int OpenUIViewAsync(string uiViewName, object userData = null);
int OpenUIViewByAssetSync(string assetLocation, object userData = null);
int OpenUIViewByAssetAsync(string assetLocation, object userData = null);
```

名称和资源地址均按原字符串精确匹配。资源地址入口在整个已加载注册表中必须匹配唯一一行；地址被多个配置共用时，返回 `-1`，应改用名称字符串入口。分组始终从所选行的 `UIGroupName` 读取，没有调用参数筛选或覆盖。没有配置时不使用 `false/true` 默认开关继续打开。

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
- 按类型打开：`OpenUIViewSync<T>()` / `OpenUIViewAsync<T>()`
- 按表内 Name 打开：`OpenUIViewSync(string uiViewName, object userData = null)` / `OpenUIViewAsync(string uiViewName, object userData = null)`
- 按唯一资源地址打开：`OpenUIViewByAssetSync(...)` / `OpenUIViewByAssetAsync(...)`
- 异步打开失败：`OnOpenUIViewFail(serialID, assetLocation, errorMessage)`
- 关闭：`CloseUIView(...)` / `CloseAllLoadedUIViews(...)` / `CloseAllLoadingUIViews()`
- 查询：`GetUIView(...)` / `HasUIView(...)` / `IsLoadingUIView(...)`
- 分组：`AddUIGroup(...)` / `GetUIGroup(...)`

## 变更影响面

如果这里的契约变化，会直接影响：

- [UIComponent.md](../../UIComponent.md)
- [UIManager.md](../UIManager.md)
- 所有依赖 `Nova.UI` 或 `UIComponent` 的业务代码

尤其高风险的是：

- 所有打开入口是否使用同一个注册表和表内行为配置
- `serialID` 语义是否变化
- “关闭加载中视图”与“关闭已加载视图”的边界是否变化
- 分组必须先注册的前置条件是否变化
- 实例池容量、过期时间等调优项与单个 UI 行的 `InObjectPools` 是否明确区分

`OpenUIViewAsync` 和 `OpenUIViewByAssetAsync` 的非负返回值表示请求已创建，不等于视图已经打开。注册表查找等立即失败仍返回 `-1`，此时没有请求编号，也不触发异步失败事件；
请求创建后的资源加载或实例化失败由 `OnOpenUIViewFail` 报告。主动关闭仍在加载的视图属于取消，不触发失败事件。

## Breaking：调用与自定义实现迁移

- 旧版带 `pauseCoveredUIView` 或 `inObjectPools` 参数的泛型重载保留签名并标记 `[Obsolete]`。两个参数不再覆盖 UI 表配置；请把所需行为写回表格，并改用不带开关的泛型重载。
- 旧版所有非泛型“资源地址 + 分组”重载均移除，不保留字符串开关包装。按地址打开应改为 `OpenUIViewByAssetSync/Async(assetLocation, userData)`；地址重复时，改为 `OpenUIViewSync/Async(uiViewName, userData)` 按 `Name` 打开。
- 特别检查旧 `(string, string)` 调用：现在它绑定名称入口，第二个字符串是 `userData`，不会再作为分组。必须显式迁移调用，不能依靠自动识别名称或地址维持旧行为。
- `IUIManager` 新增上述两个名称字符串重载和两个 `ByAsset` 方法，项目自定义实现需全部补齐。`UIManagerBase` 是框架内部类型，外部实现不能依靠该基类自动补方法；移除对旧重载的依赖后重新编译自定义实现。
- 自定义实现也应先取得唯一配置行，再将该行的资源、分组、暂停和池化配置交给实例链。保留旧泛型 bool 签名时应忽略开关值并转发到按类型查表入口，不能留下绕表入口。

## 相关实现

关键源码：

- [IUIManager.cs](../../../../../../Scripts/Runtime/Modules/UI/Managers/UIManager/Interfaces/IUIManager.cs)

相关文档：

- [UIManager.md](../UIManager.md)
- [UIComponent.md](../../UIComponent.md)
- [UIManagerConfig.md](../Definitions/UIManagerConfig.md)
- [UIView.md](../../Definitions/UIView.md)
- [UIGroupHelper.md](../../UIGroupHelper/UIGroupHelper.md)
